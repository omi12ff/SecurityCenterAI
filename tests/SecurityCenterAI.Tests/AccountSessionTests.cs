using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class AccountSessionTests
{
    [Fact]
    public async Task ExpiredAccessToken_CannotReadDataButItsActiveRefreshCanRenewTheSession()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var auth = await factory.RegisterAsync(client, AccountCredentials.New());
        factory.Clock.Advance(auth.ExpiresAtUtc - factory.Clock.GetUtcNow().UtcDateTime + TimeSpan.FromMinutes(1));
        using var expiredRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/dashboard", auth.AccessToken);
        using var expired = await client.SendAsync(expiredRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);

        using var renewal = await client.PostAsJsonAsync("/api/auth/refresh", new { auth.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, renewal.StatusCode);
        var renewed = await renewal.Content.ReadFromJsonAsync<AccountAuthentication>();
        Assert.NotNull(renewed);
        using var renewedRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/dashboard", renewed.AccessToken);
        using var active = await client.SendAsync(renewedRequest);
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
    }

    [Fact]
    public async Task SignedAccessToken_CannotUseASessionOwnedByAnotherAccount()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var own = await factory.RegisterAsync(client, AccountCredentials.New());
        var other = await factory.RegisterAsync(client, AccountCredentials.New());
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownUser = await database.Users.SingleAsync(user => user.Id == own.User.Id);
        var otherSession = await database.UserSessions.SingleAsync(session => session.UserId == other.User.Id);
        var issuer = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var (mismatched, _) = issuer.Create(ownUser, otherSession.Id);
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/dashboard", mismatched);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesAndReplayRevokesTheSessionFamily()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var original = await factory.RegisterAsync(client, account);

        using var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { original.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = await refresh.Content.ReadFromJsonAsync<AccountAuthentication>();
        Assert.NotNull(rotated);
        Assert.True(rotated.RefreshToken != original.RefreshToken, "Refresh credentials must rotate.");
        Assert.Equal(original.User.Id, rotated.User.Id);
        using var replay = await client.PostAsJsonAsync("/api/auth/refresh", new { original.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        using var deniedRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new { rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, deniedRefresh.StatusCode);
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", rotated.AccessToken);
        using var deniedAccess = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, deniedAccess.StatusCode);
    }

    [Fact]
    public async Task ExpiredRefreshCredential_CannotCreateASession()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var auth = await factory.RegisterAsync(client, AccountCredentials.New());
        factory.Clock.Advance(TimeSpan.FromDays(31));
        using var expired = await client.PostAsJsonAsync("/api/auth/refresh", new { auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", auth.AccessToken);
        using var expiredSession = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, expiredSession.StatusCode);
    }

    [Fact]
    public async Task Logout_AcceptsAHistoricalRefreshCredentialAndRevokesTheRotatedSession()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var original = await factory.RegisterAsync(client, AccountCredentials.New());
        using var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { original.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = await refresh.Content.ReadFromJsonAsync<AccountAuthentication>();
        Assert.NotNull(rotated);
        using var logout = await client.PostAsJsonAsync("/api/auth/logout", new { original.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", rotated.AccessToken);
        using var access = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, access.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesAccessAndRefreshForOnlyThatSession_AndIsIdempotent()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var original = await factory.RegisterAsync(client, account);
        using var secondLogin = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password });
        Assert.Equal(HttpStatusCode.OK, secondLogin.StatusCode);
        var second = await secondLogin.Content.ReadFromJsonAsync<AccountAuthentication>();
        Assert.NotNull(second);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var logout = await client.PostAsJsonAsync("/api/auth/logout", new { original.RefreshToken });
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }
        using var oldRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", original.AccessToken);
        using var oldAccess = await client.SendAsync(oldRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, oldAccess.StatusCode);
        using var oldRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new { original.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
        using var activeRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", second.AccessToken);
        using var active = await client.SendAsync(activeRequest);
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
    }

    [Fact]
    public async Task LogoutAll_RevokesEveryOwnSessionWithoutAffectingAnotherAccount()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var first = await factory.RegisterAsync(client, account);
        var other = await factory.RegisterAsync(client, AccountCredentials.New());
        using var secondLogin = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password });
        Assert.Equal(HttpStatusCode.OK, secondLogin.StatusCode);
        var second = await secondLogin.Content.ReadFromJsonAsync<AccountAuthentication>();
        Assert.NotNull(second);
        using var logoutRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, "/api/auth/logout-all", first.AccessToken);
        using var logout = await client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        foreach (var auth in new[] { first, second })
        {
            using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", auth.AccessToken);
            using var access = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, access.StatusCode);
            using var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { auth.RefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
        using var otherRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", other.AccessToken);
        using var otherAccess = await client.SendAsync(otherRequest);
        Assert.Equal(HttpStatusCode.OK, otherAccess.StatusCode);
    }

    [Theory]
    [InlineData("me", "GET")]
    [InlineData("resend-verification", "POST")]
    [InlineData("logout-all", "POST")]
    public async Task AccountOperations_RequireAuthentication(string path, string method)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/auth/{path}");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
