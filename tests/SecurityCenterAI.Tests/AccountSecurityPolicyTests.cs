using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class AccountSecurityPolicyTests
{
    [Theory]
    [InlineData("TestPassword12")]
    [InlineData("PASSWORDPASSWORD")]
    [InlineData("123456789012345")]
    [InlineData("🔒🔒🔒🔒🔒🔒🔒🔒")]
    public async Task RejectedRegistrationCannotCreateCredentialsTokensOrEmail(string candidate)
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        using var rejected = await client.PostAsJsonAsync("/api/auth/register", new { account.Name, account.Email, password = candidate });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.DoesNotContain(candidate, await rejected.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Users.ToListAsync()); Assert.Empty(await db.UserSessions.ToListAsync());
        Assert.Empty(await db.AccountActionTokens.ToListAsync()); Assert.Empty(factory.Email.Messages);
    }

    [Fact]
    public async Task RejectedReplacementsPreservePasswordSessionsRecoveryAndSecurityEvents()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
        using var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email });
        var token = factory.Email.TokenFor(account.Email, AccountEmailPurpose.PasswordReset);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var original = await db.Users.AsNoTracking().SingleAsync(); var events = await db.SecurityActivities.CountAsync();
        using var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "passwordpassword" });
        using var change = await AccountPasswordChangeTests.ChangeAsync(client, owner, account.Password, "passwordpassword");
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode); Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
        var unchanged = await db.Users.AsNoTracking().SingleAsync();
        Assert.Equal(original.PasswordHash, unchanged.PasswordHash); Assert.Equal(original.ConcurrencyStamp, unchanged.ConcurrencyStamp);
        Assert.Null((await db.AccountActionTokens.AsNoTracking().SingleAsync(item => item.Purpose == AccountService.PasswordResetPurpose)).ConsumedAtUtc);
        Assert.Null((await db.UserSessions.AsNoTracking().SingleAsync()).RevokedAtUtc);
        Assert.Equal(events, await db.SecurityActivities.CountAsync());
        const string replacement = "  frase nueva ñ con espacios  ";
        using var accepted = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = replacement });
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        using var trimmed = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, password = replacement.Trim() });
        Assert.Equal(HttpStatusCode.Unauthorized, trimmed.StatusCode);
        await AccountSessionManagementTests.LoginAsync(client, account with { Password = replacement });
    }

    [Fact]
    public async Task ExistingShortPasswordCanStillLoginAndActAsCurrentPassword()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var user = await db.Users.SingleAsync();
            user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, "legacy8!");
            await db.SaveChangesAsync();
        }
        var legacy = await AccountSessionManagementTests.LoginAsync(client, account with { Password = "legacy8!" });
        using var changed = await AccountPasswordChangeTests.ChangeAsync(client, legacy, "legacy8!");
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        await AccountSessionManagementTests.LoginAsync(client, account with { Password = AccountPasswordChangeTests.NewPassword });
    }

    [Fact]
    public async Task SessionLimitPreservesExistingAccessAndCanBeFreedByRevocationOrExpiry()
    {
        using var factory = new AccountApiFactory(maxActiveSessions: 2); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
        var other = await AccountSessionManagementTests.LoginAsync(client, account);
        var foreign = await factory.RegisterAsync(client, AccountCredentials.New());
        using var wrong = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, password = "not the correct password" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        using var blocked = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var body = await blocked.Content.ReadAsStringAsync();
        foreach (var secret in new[] { account.Email, account.Password, owner.AccessToken, other.RefreshToken }) Assert.DoesNotContain(secret, body);
        using var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new { owner.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var rotated = (await refreshed.Content.ReadFromJsonAsync<AccountAuthentication>())!;
        var list = await AccountSessionManagementTests.ReadAsync(client, rotated.AccessToken);
        Assert.Equal(2, list.MaxActiveSessions); Assert.Equal(2, list.Sessions.Count);
        using var revoked = await AccountSessionManagementTests.RevokeAsync(client, rotated.AccessToken, AccountSessionManagementTests.SessionId(other));
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        await AccountSessionManagementTests.LoginAsync(client, account);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var current = await db.UserSessions.SingleAsync(item => item.Id == AccountSessionManagementTests.SessionId(rotated));
            current.ExpiresAtUtc = factory.Clock.GetUtcNow().UtcDateTime; await db.SaveChangesAsync();
        }
        var afterExpiry = await AccountSessionManagementTests.LoginAsync(client, account);
        Assert.Equal(2, (await AccountSessionManagementTests.ReadAsync(client, afterExpiry.AccessToken)).Sessions.Count);
        Assert.Single((await AccountSessionManagementTests.ReadAsync(client, foreign.AccessToken)).Sessions);
    }
}
