using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class AccountSessionManagementTests
{
    internal const string Path = "/api/account/sessions";
    internal static Guid SessionId(AccountAuthentication auth) => Guid.Parse(new JwtSecurityTokenHandler()
        .ReadJwtToken(auth.AccessToken).Claims.Single(claim => claim.Type == "sid").Value);

    internal static async Task<AccountAuthentication> LoginAsync(HttpClient client, AccountCredentials account)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountAuthentication>())!;
    }

    internal static Task<HttpResponseMessage> RevokeAsync(HttpClient client, string token, Guid id, bool confirmed = true)
    {
        var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, $"{Path}/{id}/revoke", token);
        request.Content = JsonContent.Create(new { confirmed });
        return SendAndDisposeAsync(client, request);
    }

    private static async Task<HttpResponseMessage> SendAndDisposeAsync(HttpClient client, HttpRequestMessage request)
    { using (request) return await client.SendAsync(request); }

    internal static async Task<AccountSessionListResponse> ReadAsync(HttpClient client, string token, string query = "")
    {
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, Path + query, token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<AccountSessionListResponse>())!;
    }

    [Fact]
    public async Task InventoryRequiresBearerAndReturnsOnlyOwnerDatesAndCurrentSessionFlag()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var credentials = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, credentials);
        var foreign = await factory.RegisterAsync(client, AccountCredentials.New());
        var list = await ReadAsync(client, owner.AccessToken, "?userId=" + foreign.User.Id);
        Assert.Equal(SessionId(owner), Assert.Single(list.Sessions).Id);
        Assert.True(list.Sessions[0].IsCurrent);
        Assert.Equal(factory.Clock.GetUtcNow().UtcDateTime, list.Sessions[0].CreatedAtUtc);
        var json = JsonSerializer.Serialize(list);
        foreach (var value in new[] { credentials.Email, credentials.Password, owner.AccessToken, owner.RefreshToken,
            owner.User.Id.ToString(), SessionId(foreign).ToString() }) Assert.DoesNotContain(value, json);
        using var plain = await client.GetAsync(Path); Assert.Equal(HttpStatusCode.Unauthorized, plain.StatusCode);
        using var deviceRequest = new HttpRequestMessage(HttpMethod.Get, Path);
        deviceRequest.Headers.Authorization = new("Device", new string('A', 43));
        using var device = await client.SendAsync(deviceRequest); Assert.Equal(HttpStatusCode.Unauthorized, device.StatusCode);
    }

    [Fact]
    public async Task InventoryFiltersInactiveSessionsPaginatesAndKeepsCurrentFirstEvenWhenOlder()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var owner = await factory.RegisterAsync(client, AccountCredentials.New());
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var index = 1; index <= 65; index++) db.UserSessions.Add(new() { UserId = owner.User.Id,
                CreatedAtUtc = now.AddMinutes(index), ExpiresAtUtc = now.AddDays(5) });
            db.UserSessions.Add(new() { UserId = owner.User.Id, CreatedAtUtc = now.AddDays(-2), ExpiresAtUtc = now });
            db.UserSessions.Add(new() { UserId = owner.User.Id, CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(5), RevokedAtUtc = now });
            await db.SaveChangesAsync();
        }
        var first = await ReadAsync(client, owner.AccessToken);
        var second = await ReadAsync(client, owner.AccessToken, "?page=2");
        Assert.Equal(50, first.Sessions.Count); Assert.Equal(50, first.Limit); Assert.True(first.HasMore);
        Assert.Equal(SessionId(owner), first.Sessions[0].Id); Assert.True(first.Sessions[0].IsCurrent);
        Assert.Equal(16, second.Sessions.Count); Assert.False(second.HasMore); Assert.Equal(2, second.Page);
        Assert.DoesNotContain(second.Sessions, item => item.IsCurrent);
        Assert.Equal(66, first.Sessions.Concat(second.Sessions).Select(item => item.Id).Distinct().Count());
        Assert.Empty((await ReadAsync(client, owner.AccessToken, "?page=3")).Sessions);
        foreach (var invalid in new[] { "0", "10001", "invalid", "-1" })
        {
            using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, Path + "?page=" + invalid, owner.AccessToken);
            using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task RevokingOneSessionBlocksItsAccessAndRotatedRefreshAndPreservesOtherSessionsAndDeviceAccess()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, account);
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var device = await DeviceApiTestSupport.PairAsync(client, code.PairingCode);
        var other = await LoginAsync(client, account);
        using var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { other.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = (await refresh.Content.ReadFromJsonAsync<AccountAuthentication>())!;
        using var revoked = await RevokeAsync(client, owner.AccessToken, SessionId(other));
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.False((await revoked.Content.ReadFromJsonAsync<RevokeAccountSessionResponse>())!.CurrentSession);
        foreach (var token in new[] { other.AccessToken, rotated.AccessToken })
        {
            using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", token);
            using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using var renewal = await client.PostAsJsonAsync("/api/auth/refresh", new { rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, renewal.StatusCode);
        Assert.Single((await ReadAsync(client, owner.AccessToken)).Sessions);
        using var heartbeat = new HttpRequestMessage(HttpMethod.Post, "/api/agent/heartbeat");
        heartbeat.Headers.Authorization = new("Device", device.DeviceToken);
        heartbeat.Content = JsonContent.Create(new { agentVersion = "0.4.1-preview.1", platform = "Windows 11" });
        using var beat = await client.SendAsync(heartbeat); Assert.True(beat.IsSuccessStatusCode);
    }

    [Fact]
    public async Task RevocationChecksOwnershipAndConfirmationAndRecordsRepeatedRequestsOnlyOnce()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
        var target = await LoginAsync(client, account);
        var foreign = await factory.RegisterAsync(client, AccountCredentials.New());
        using var denied = await RevokeAsync(client, owner.AccessToken, SessionId(target), false);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        using var absent = await RevokeAsync(client, owner.AccessToken, Guid.NewGuid());
        using var other = await RevokeAsync(client, owner.AccessToken, SessionId(foreign));
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        Assert.Equal(await absent.Content.ReadAsStringAsync(), await other.Content.ReadAsStringAsync());
        Assert.Equal(2, (await ReadAsync(client, owner.AccessToken)).Sessions.Count);
        for (var iteration = 0; iteration < 2; iteration++)
        { using var response = await RevokeAsync(client, owner.AccessToken, SessionId(target)); Assert.Equal(HttpStatusCode.OK, response.StatusCode); }
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.SecurityActivities.CountAsync(item => item.UserId == owner.User.Id && item.Kind == SecurityActivityKind.SessionRevoked));
        Assert.Single((await ReadAsync(client, foreign.AccessToken)).Sessions);
    }

    [Fact]
    public async Task ClosingCurrentSessionReturnsTrustedFlagAndInvalidatesItWithoutClosingAnotherSession()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var current = await factory.RegisterAsync(client, account);
        var retained = await LoginAsync(client, account);
        using var response = await RevokeAsync(client, current.AccessToken, SessionId(current));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<RevokeAccountSessionResponse>())!.CurrentSession);
        using var denied = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, Path, current.AccessToken);
        using var blocked = await client.SendAsync(denied); Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);
        using var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { current.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Single((await ReadAsync(client, retained.AccessToken)).Sessions);
    }

    [Fact]
    public async Task SessionInventoryAndMutationShareTheAccountRateLimit()
    {
        using var factory = new AccountApiFactory(devicePermitLimit: 1); using var client = factory.CreateApiClient();
        var owner = await factory.RegisterAsync(client, AccountCredentials.New());
        await ReadAsync(client, owner.AccessToken);
        using var response = await RevokeAsync(client, owner.AccessToken, SessionId(owner));
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Null((await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserSessions.SingleAsync()).RevokedAtUtc);
    }
}
