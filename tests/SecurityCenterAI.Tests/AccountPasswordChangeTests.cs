using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class AccountPasswordChangeTests
{
    internal const string Path = "/api/account/password";
    internal const string NewPassword = "Nueva clave ficticia 2026!";
    internal static async Task<HttpResponseMessage> ChangeAsync(HttpClient client, AccountAuthentication auth,
        string current, string next = NewPassword, bool confirmed = true)
    {
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, Path, auth.AccessToken);
        request.Content = JsonContent.Create(new { currentPassword = current, newPassword = next, confirmed });
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task ChangeInvalidatesAllOwnWebAccessAndRecoveryLinksButPreservesOtherAccountsAndDeviceAccess()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, account);
        var foreign = await factory.RegisterAsync(client, AccountCredentials.New());
        var second = await AccountSessionManagementTests.LoginAsync(client, account);
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var device = await DeviceApiTestSupport.PairAsync(client, code.PairingCode);
        using var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email });
        var reset = factory.Email.TokenFor(account.Email, AccountEmailPurpose.PasswordReset);
        using var changed = await ChangeAsync(client, owner, account.Password);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.True(changed.Headers.CacheControl?.NoStore);
        foreach (var auth in new[] { owner, second })
        {
            using var me = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", auth.AccessToken);
            using var denied = await client.SendAsync(me); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            using var renewal = await client.PostAsJsonAsync("/api/auth/refresh", new { auth.RefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, renewal.StatusCode);
        }
        using var oldLink = await client.PostAsJsonAsync("/api/auth/reset-password", new { token = reset, password = "Otro valor ficticio!" });
        Assert.Equal(HttpStatusCode.BadRequest, oldLink.StatusCode);
        using var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        await AccountSessionManagementTests.LoginAsync(client, account with { Password = NewPassword });
        Assert.Single((await AccountSessionManagementTests.ReadAsync(client, foreign.AccessToken)).Sessions);
        using var heartbeat = new HttpRequestMessage(HttpMethod.Post, "/api/agent/heartbeat");
        heartbeat.Headers.Authorization = new("Device", device.DeviceToken);
        heartbeat.Content = JsonContent.Create(new { agentVersion = "0.4.1-preview.1", platform = "Windows 11" });
        using var beat = await client.SendAsync(heartbeat); Assert.True(beat.IsSuccessStatusCode);
        var notice = Assert.Single(factory.Email.Messages.Where(item => item.Purpose == AccountEmailPurpose.PasswordChanged));
        Assert.Equal(account.Email, notice.Recipient); Assert.Equal("", notice.Token);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.SecurityActivities.CountAsync(item => item.UserId == owner.User.Id && item.Kind == SecurityActivityKind.PasswordChanged));
        Assert.NotEqual(NewPassword, (await db.Users.SingleAsync(item => item.Id == owner.User.Id)).PasswordHash);
    }

    [Fact]
    public async Task WrongCurrentPasswordUsesSharedLockoutWithoutRevokingSessionsOrRecordingSuccess()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
        for (var index = 0; index < factory.SecurityOptions.MaxFailedLoginAttempts; index++)
        { using var denied = await ChangeAsync(client, owner, "incorrecta"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); }
        using var locked = await ChangeAsync(client, owner, account.Password);
        Assert.Equal(HttpStatusCode.Forbidden, locked.StatusCode);
        Assert.Single((await AccountSessionManagementTests.ReadAsync(client, owner.AccessToken)).Sessions);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(); Assert.True(user.LockoutEndUtc > factory.Clock.GetUtcNow().UtcDateTime);
        Assert.Equal(factory.SecurityOptions.MaxFailedLoginAttempts, user.FailedLoginAttempts);
        Assert.False(await db.SecurityActivities.AnyAsync(item => item.Kind == SecurityActivityKind.PasswordChanged));
        Assert.DoesNotContain(factory.Email.Messages, item => item.Purpose == AccountEmailPurpose.PasswordChanged);
    }

    [Theory]
    [InlineData("short", true)]
    [InlineData("valid new password", false)]
    [InlineData("        ", true)]
    public async Task InvalidInputDoesNotChangeTheAccount(string next, bool confirmed)
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
        using var response = await ChangeAsync(client, owner, account.Password, next, confirmed);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(account.Password, await response.Content.ReadAsStringAsync());
        Assert.Single((await AccountSessionManagementTests.ReadAsync(client, owner.AccessToken)).Sessions);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync()).FailedLoginAttempts);
    }

    [Fact]
    public async Task ChangeRejectsSamePasswordAndIgnoresForeignOwnerInTheBody()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
        var foreignAccount = AccountCredentials.New(); var foreign = await factory.RegisterAsync(client, foreignAccount);
        using var same = await ChangeAsync(client, owner, account.Password, account.Password);
        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);
        using var forged = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, Path + "?userId=" + foreign.User.Id, owner.AccessToken);
        forged.Content = JsonContent.Create(new { userId = foreign.User.Id, currentPassword = foreignAccount.Password, newPassword = NewPassword, confirmed = true });
        using var rejected = await client.SendAsync(forged); Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        await AccountSessionManagementTests.LoginAsync(client, foreignAccount);
        using var anonymous = await client.PostAsJsonAsync(Path, new { currentPassword = account.Password, newPassword = NewPassword, confirmed = true });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var device = new HttpRequestMessage(HttpMethod.Post, Path);
        device.Headers.Authorization = new("Device", new string('A', 43));
        device.Content = JsonContent.Create(new { currentPassword = account.Password, newPassword = NewPassword, confirmed = true });
        using var denied = await client.SendAsync(device); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task ServiceRechecksTheActingSessionAndPreservesLegacyPasswordWithoutTrimming()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var owner = await factory.RegisterAsync(client, AccountCredentials.New());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.UserSessions.SingleAsync(); session.RevokedAtUtc = factory.Clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync();
            var error = await Assert.ThrowsAsync<DeviceOperationException>(() => scope.ServiceProvider.GetRequiredService<AccountPasswordService>()
                .ChangeAsync(owner.User.Id, session.Id, new("invalid", NewPassword, true), CancellationToken.None));
            Assert.Equal(401, error.StatusCode);
            session.RevokedAtUtc = null;
            var user = await db.Users.SingleAsync(); user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, " p ");
            await db.SaveChangesAsync();
        }
        using var changed = await ChangeAsync(client, owner, " p ", "  nueva contraseña  ");
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        using var newLogin = await client.PostAsJsonAsync("/api/auth/login", new { email = owner.User.Email, password = "  nueva contraseña  " });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task RateLimitAndFailureToStageTheNoticePreventTheChange()
    {
        using var limited = new AccountApiFactory(accountPasswordPermitLimit: 1); using var client = limited.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await limited.RegisterAsync(client, account);
        using var wrong = await ChangeAsync(client, owner, "incorrecta"); Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        using var blocked = await ChangeAsync(client, owner, account.Password); Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        using var factory = new AccountApiFactory();
        using var otherClient = factory.CreateApiClient();
        var own = await factory.RegisterAsync(otherClient, account);
        factory.Email.RejectedPurpose = AccountEmailPurpose.PasswordChanged;
        using var changed = await ChangeAsync(otherClient, own, account.Password); Assert.Equal(HttpStatusCode.InternalServerError, changed.StatusCode);
        await AccountSessionManagementTests.LoginAsync(otherClient, account);
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", own.AccessToken);
        using var unchanged = await otherClient.SendAsync(request); Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
    }
}
