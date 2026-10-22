using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using SecurityCenterAI.Api.Services;

namespace SecurityCenterAI.Tests;

public sealed class AccountAbuseProtectionTests
{
    [Fact]
    public async Task Login_IsLockedAfterRepeatedFailures_AndWorksAfterTheLockoutExpires()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        await factory.RegisterAsync(client, account);
        var incorrect = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

        for (var failure = 0; failure < factory.SecurityOptions.MaxFailedLoginAttempts; failure++)
        {
            using var attempt = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, password = incorrect });
            Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
        }
        using var locked = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);

        factory.Clock.Advance(TimeSpan.FromMinutes(factory.SecurityOptions.LockoutMinutes + 1));
        using var unlocked = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password });
        Assert.Equal(HttpStatusCode.OK, unlocked.StatusCode);
    }

    [Fact]
    public async Task SuccessfulLogin_ResetsTheConsecutiveFailureCounter()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        await factory.RegisterAsync(client, account);
        var incorrect = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

        for (var round = 0; round < 2; round++)
        {
            for (var failure = 0; failure < factory.SecurityOptions.MaxFailedLoginAttempts - 1; failure++)
            {
                using var attempt = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, password = incorrect });
                Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
            }
            using var success = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password });
            Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        }
    }

    [Fact]
    public async Task RepeatedRecoveryRequests_KeepTheGenericResponseAndHonorEmailCooldown()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        await factory.RegisterAsync(client, account);

        using var first = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email });
        using var cooldown = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cooldown.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await cooldown.Content.ReadAsStringAsync());
        Assert.Equal(1, factory.Email.Messages.Count(message => message.Purpose == AccountEmailPurpose.PasswordReset));

        factory.Clock.Advance(TimeSpan.FromSeconds(factory.SecurityOptions.EmailCooldownSeconds + 1));
        using var later = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email });
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
        Assert.Equal(2, factory.Email.Messages.Count(message => message.Purpose == AccountEmailPurpose.PasswordReset));
    }

    [Fact]
    public async Task PublicAuthentication_HasAnHttpRateLimitIndependentOfAccountLockout()
    {
        using var factory = new AccountApiFactory(authPermitLimit: 2);
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        for (var request = 0; request < 2; request++)
        {
            using var allowed = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password });
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }
        using var spoofed = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { account.Email, account.Password })
        };
        spoofed.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.123");
        using var limited = await client.SendAsync(spoofed);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
    }
}
