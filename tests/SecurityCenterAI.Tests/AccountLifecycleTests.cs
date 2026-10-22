using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class AccountLifecycleTests
{
    [Fact]
    public async Task RefreshAndActionCredentials_ArePersistedAsHashesRatherThanRawTokens()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var auth = await factory.RegisterAsync(client, account);
        var verification = factory.Email.TokenFor(account.Email, AccountEmailPurpose.EmailVerification);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refresh = await database.RefreshCredentials.SingleAsync();
        var action = await database.AccountActionTokens.SingleAsync();
        var user = await database.Users.SingleAsync();
        var expectedRefreshHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(auth.RefreshToken)));
        var expectedActionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(verification)));
        Assert.True(refresh.TokenHash == expectedRefreshHash);
        Assert.True(action.TokenHash == expectedActionHash);
        Assert.True(user.PasswordHash != account.Password);
    }

    [Fact]
    public async Task Registration_SendsVerificationWithoutReturningItsToken_AndMeReportsTheAccount()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var auth = await factory.RegisterAsync(client, account);
        Assert.False(auth.User.EmailVerified);
        Assert.Equal(account.Email, auth.User.Email);
        Assert.True(auth.ExpiresAtUtc > factory.Clock.GetUtcNow().UtcDateTime);
        Assert.True(auth.RefreshExpiresAtUtc > auth.ExpiresAtUtc);
        var verification = factory.Email.TokenFor(account.Email, AccountEmailPurpose.EmailVerification);
        Assert.True(!string.IsNullOrWhiteSpace(verification));
        Assert.True(verification != auth.AccessToken && verification != auth.RefreshToken);

        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", auth.AccessToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var current = await response.Content.ReadFromJsonAsync<AccountUser>();
        Assert.NotNull(current);
        Assert.Equal(auth.User.Id, current.Id);
        Assert.False(current.EmailVerified);
        var serialized = await response.Content.ReadAsStringAsync();
        Assert.False(serialized.Contains(account.Password, StringComparison.Ordinal));
        Assert.False(serialized.Contains("passwordHash", StringComparison.OrdinalIgnoreCase));
        Assert.False(serialized.Contains(verification, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Verification_IsSingleUseAndOnlyVerifiesItsOwnAccount()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var otherAccount = AccountCredentials.New();
        var auth = await factory.RegisterAsync(client, account);
        var other = await factory.RegisterAsync(client, otherAccount);
        var token = factory.Email.TokenFor(account.Email, AccountEmailPurpose.EmailVerification);

        using var first = await client.PostAsJsonAsync("/api/auth/verify-email", new { token });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        using var repeated = await client.PostAsJsonAsync("/api/auth/verify-email", new { token });
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);

        using var ownRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", auth.AccessToken);
        using var ownResponse = await client.SendAsync(ownRequest);
        var own = await ownResponse.Content.ReadFromJsonAsync<AccountUser>();
        Assert.NotNull(own);
        Assert.True(own.EmailVerified);
        using var otherRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", other.AccessToken);
        using var otherResponse = await client.SendAsync(otherRequest);
        var unchanged = await otherResponse.Content.ReadFromJsonAsync<AccountUser>();
        Assert.NotNull(unchanged);
        Assert.False(unchanged.EmailVerified);
    }

    [Theory]
    [InlineData("verify-email")]
    [InlineData("reset-password")]
    public async Task ActionTokens_RejectMissingMalformedAndExpiredTokens(string action)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        await factory.RegisterAsync(client, account);
        string token;
        if (action == "reset-password")
        {
            using var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email });
            Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
            token = factory.Email.TokenFor(account.Email, AccountEmailPurpose.PasswordReset);
        }
        else token = factory.Email.TokenFor(account.Email, AccountEmailPurpose.EmailVerification);

        foreach (var invalid in new[] { "", "invalid-token" })
        {
            using var rejected = await client.PostAsJsonAsync($"/api/auth/{action}",
                new { token = invalid, password = account.Password });
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }

        factory.Clock.Advance(TimeSpan.FromDays(2));
        using var expired = await client.PostAsJsonAsync($"/api/auth/{action}",
            new { token, password = account.Password });
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_HasTheSameResponseForKnownAndUnknownAccounts()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        await factory.RegisterAsync(client, account);
        var mailCount = factory.Email.Messages.Length;

        using var known = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email });
        using var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new { email = AccountCredentials.New().Email });
        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.Equal(mailCount + 1, factory.Email.Messages.Length);
        var resetToken = factory.Email.TokenFor(account.Email, AccountEmailPurpose.PasswordReset);
        Assert.False((await known.Content.ReadAsStringAsync()).Contains(resetToken, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResetPassword_IsSingleUseRevokesExistingSessionsAndPreservesOtherAccounts()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var otherAccount = AccountCredentials.New();
        var auth = await factory.RegisterAsync(client, account);
        var other = await factory.RegisterAsync(client, otherAccount);
        using var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        var token = factory.Email.TokenFor(account.Email, AccountEmailPurpose.PasswordReset);
        var replacement = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

        using var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = replacement });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using var replay = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = replacement });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        using var newLogin = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, password = replacement });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        using var oldAccess = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", auth.AccessToken);
        using var oldAccessResponse = await client.SendAsync(oldAccess);
        Assert.Equal(HttpStatusCode.Unauthorized, oldAccessResponse.StatusCode);
        using var oldRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new { auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
        using var otherRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", other.AccessToken);
        using var otherResponse = await client.SendAsync(otherRequest);
        Assert.Equal(HttpStatusCode.OK, otherResponse.StatusCode);
    }

    [Fact]
    public async Task VerificationAndResetTokens_AreNotInterchangeable()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        await factory.RegisterAsync(client, account);
        var verification = factory.Email.TokenFor(account.Email, AccountEmailPurpose.EmailVerification);
        using var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        var reset = factory.Email.TokenFor(account.Email, AccountEmailPurpose.PasswordReset);

        using var wrongReset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { token = verification, password = account.Password });
        Assert.Equal(HttpStatusCode.BadRequest, wrongReset.StatusCode);
        using var wrongVerification = await client.PostAsJsonAsync("/api/auth/verify-email", new { token = reset });
        Assert.Equal(HttpStatusCode.BadRequest, wrongVerification.StatusCode);
        using var correct = await client.PostAsJsonAsync("/api/auth/verify-email", new { token = verification });
        Assert.Equal(HttpStatusCode.NoContent, correct.StatusCode);
    }

    [Fact]
    public async Task InvalidReplacementPassword_DoesNotConsumeTheResetToken()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        await factory.RegisterAsync(client, account);
        using var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        var token = factory.Email.TokenFor(account.Email, AccountEmailPurpose.PasswordReset);

        using var invalid = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { token, password = Convert.ToHexString(RandomNumberGenerator.GetBytes(2)) });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var accepted = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = account.Password });
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
    }

    [Fact]
    public async Task ResendVerification_RequiresSessionAndHonorsCooldownWithoutChangingOtherAccounts()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var auth = await factory.RegisterAsync(client, account);
        factory.Clock.Advance(TimeSpan.FromSeconds(factory.SecurityOptions.EmailCooldownSeconds + 1));

        for (var request = 0; request < 2; request++)
        {
            using var message = AccountApiFactory.AuthorizedRequest(HttpMethod.Post,
                "/api/auth/resend-verification", auth.AccessToken);
            using var response = await client.SendAsync(message);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        Assert.Equal(2, factory.Email.Messages.Count(message => message.Recipient == account.Email));
        var token = factory.Email.TokenFor(account.Email, AccountEmailPurpose.EmailVerification);
        using var verification = await client.PostAsJsonAsync("/api/auth/verify-email", new { token });
        Assert.Equal(HttpStatusCode.NoContent, verification.StatusCode);
        factory.Clock.Advance(TimeSpan.FromSeconds(factory.SecurityOptions.EmailCooldownSeconds + 1));
        using var repeated = AccountApiFactory.AuthorizedRequest(HttpMethod.Post,
            "/api/auth/resend-verification", auth.AccessToken);
        using var afterVerification = await client.SendAsync(repeated);
        Assert.Equal(HttpStatusCode.OK, afterVerification.StatusCode);
        Assert.Equal(2, factory.Email.Messages.Count(message => message.Recipient == account.Email));
    }
}
