using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class AccountEmailChangeTests
{
    internal const string Path = "/api/account/email";
    internal static async Task<AccountAuthentication> VerifiedAsync(AccountApiFactory factory, HttpClient client, AccountCredentials account)
    {
        var auth = await factory.RegisterAsync(client, account);
        using var verify = await client.PostAsJsonAsync("/api/auth/verify-email", new { token = factory.Email.TokenFor(account.Email, AccountEmailPurpose.EmailVerification) });
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
        factory.Clock.Advance(TimeSpan.FromSeconds(61));
        return auth;
    }
    internal static async Task<HttpResponseMessage> PostAsync(HttpClient client, string action, string token, object body)
    {
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, Path + "/" + action, token);
        request.Content = JsonContent.Create(body); return await client.SendAsync(request);
    }
    internal static async Task<PendingEmailChangeResponse?> PendingAsync(HttpClient client, string token)
    {
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, Path, token);
        using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<EmailChangeStatusResponse>())!.Pending;
    }
    internal static async Task ConfirmBothAsync(AccountApiFactory factory, HttpClient client, string current, string next)
    {
        foreach (var pair in new[] { ("current", current, AccountEmailPurpose.EmailChangeCurrent), ("new", next, AccountEmailPurpose.EmailChangeNew) })
        {
            using var result = await client.PostAsJsonAsync(Path + "/confirm-" + pair.Item1,
                new { token = factory.Email.TokenFor(pair.Item2, pair.Item3), confirmed = true });
            Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        }
    }
    internal static async Task<PendingEmailChangeResponse> BeginAsync(HttpClient client, AccountAuthentication owner, AccountCredentials account, string target)
    {
        using var sent = await PostAsync(client, "request", owner.AccessToken, new { newEmail = target, currentPassword = account.Password, confirmed = true });
        Assert.Equal(HttpStatusCode.NoContent, sent.StatusCode);
        return (await PendingAsync(client, owner.AccessToken))!;
    }

    [Fact]
    public async Task BothApprovalsAndCurrentPasswordAreRequiredBeforeTheAtomicChange()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await VerifiedAsync(factory, client, account);
        var target = AccountCredentials.New().Email; var pending = await BeginAsync(client, owner, account, "  " + target.ToUpperInvariant() + "  ");
        Assert.Equal(target, pending.NewEmail); Assert.False(pending.NewAddressConfirmed); Assert.False(pending.CurrentAddressConfirmed);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var original = await db.Users.AsNoTracking().SingleAsync();
        var device = new Device { UserId = owner.User.Id, Name = "Email change fixture", CreatedAtUtc = factory.Clock.GetUtcNow().UtcDateTime };
        db.DeviceCredentials.Add(new() { Device = device, TokenHash = OpaqueToken.Hash(OpaqueToken.Create()),
            CreatedAtUtc = factory.Clock.GetUtcNow().UtcDateTime, ExpiresAtUtc = factory.Clock.GetUtcNow().UtcDateTime.AddDays(30) });
        await db.SaveChangesAsync();
        var row = await db.EmailChangeRequests.AsNoTracking().SingleAsync();
        Assert.NotEqual(factory.Email.TokenFor(account.Email, AccountEmailPurpose.EmailChangeCurrent), row.CurrentTokenHash);
        Assert.Equal(64, row.CurrentTokenHash.Length); Assert.Equal(64, row.NewTokenHash.Length);
        using var early = await PostAsync(client, "complete", owner.AccessToken, new { requestId = pending.Id, currentPassword = account.Password, confirmed = true });
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        using var confirmCurrent = await client.PostAsJsonAsync(Path + "/confirm-current", new { token = factory.Email.TokenFor(account.Email, AccountEmailPurpose.EmailChangeCurrent), confirmed = true });
        Assert.Equal(HttpStatusCode.NoContent, confirmCurrent.StatusCode);
        using var onlyOne = await PostAsync(client, "complete", owner.AccessToken, new { requestId = pending.Id, currentPassword = account.Password, confirmed = true });
        Assert.Equal(HttpStatusCode.BadRequest, onlyOne.StatusCode);
        using var confirmNew = await client.PostAsJsonAsync(Path + "/confirm-new", new { token = factory.Email.TokenFor(target, AccountEmailPurpose.EmailChangeNew), confirmed = true });
        Assert.Equal(HttpStatusCode.NoContent, confirmNew.StatusCode);
        using var wrong = await PostAsync(client, "complete", owner.AccessToken, new { requestId = pending.Id, currentPassword = "wrong", confirmed = true });
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        using var finished = await PostAsync(client, "complete", owner.AccessToken, new { requestId = pending.Id, currentPassword = account.Password, confirmed = true, emailVerified = false, passwordHash = "forged" });
        Assert.Equal(HttpStatusCode.NoContent, finished.StatusCode);
        var changed = await db.Users.AsNoTracking().SingleAsync();
        Assert.Equal(target, changed.Email); Assert.True(changed.EmailVerified); Assert.Equal(original.PasswordHash, changed.PasswordHash);
        Assert.Empty(await db.EmailChangeRequests.AsNoTracking().ToListAsync());
        Assert.False(await db.UserSessions.AnyAsync(item => item.RevokedAtUtc == null));
        Assert.Null((await db.DeviceCredentials.AsNoTracking().SingleAsync()).RevokedAtUtc);
        Assert.Single(await db.SecurityActivities.Where(item => item.Kind == SecurityActivityKind.EmailChanged).ToListAsync());
        using var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { owner.RefreshToken }); Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        using var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password }); Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        await AccountSessionManagementTests.LoginAsync(client, account with { Email = target });
        Assert.Equal(2, factory.Email.Messages.Count(item => item.Purpose == AccountEmailPurpose.EmailChanged));
        Assert.All(factory.Email.Messages.Where(item => item.Purpose == AccountEmailPurpose.EmailChanged), item => Assert.Empty(item.Token));
    }

    [Fact]
    public async Task TokensArePurposeBoundSingleUseAndInvalidAfterReplacementCancellationOrExpiry()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await VerifiedAsync(factory, client, account);
        var target = AccountCredentials.New().Email; var first = await BeginAsync(client, owner, account, target);
        var token = factory.Email.TokenFor(account.Email, AccountEmailPurpose.EmailChangeCurrent);
        using var swapped = await client.PostAsJsonAsync(Path + "/confirm-new", new { token, confirmed = true }); Assert.Equal(HttpStatusCode.BadRequest, swapped.StatusCode);
        using var approved = await client.PostAsJsonAsync(Path + "/confirm-current", new { token, confirmed = true }); Assert.Equal(HttpStatusCode.NoContent, approved.StatusCode);
        using var replay = await client.PostAsJsonAsync(Path + "/confirm-current", new { token, confirmed = true }); Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        factory.Clock.Advance(TimeSpan.FromSeconds(61));
        var second = await BeginAsync(client, owner, account, AccountCredentials.New().Email); Assert.NotEqual(first.Id, second.Id);
        using var stale = await PostAsync(client, "cancel", owner.AccessToken, new { requestId = first.Id, confirmed = true }); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var replaced = await client.PostAsJsonAsync(Path + "/confirm-current", new { token, confirmed = true }); Assert.Equal(HttpStatusCode.BadRequest, replaced.StatusCode);
        var secondToken = factory.Email.TokenFor(account.Email, AccountEmailPurpose.EmailChangeCurrent);
        using var cancel = await PostAsync(client, "cancel", owner.AccessToken, new { requestId = second.Id, confirmed = true }); Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        using var cancelled = await client.PostAsJsonAsync(Path + "/confirm-current", new { token = secondToken, confirmed = true }); Assert.Equal(HttpStatusCode.BadRequest, cancelled.StatusCode);
        factory.Clock.Advance(TimeSpan.FromSeconds(61));
        await BeginAsync(client, owner, account, target);
        var expiredToken = factory.Email.TokenFor(target, AccountEmailPurpose.EmailChangeNew);
        factory.Clock.Advance(TimeSpan.FromMinutes(31));
        using var expired = await client.PostAsJsonAsync(Path + "/confirm-new", new { token = expiredToken, confirmed = true }); Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PasswordChangesAndRecoveryCancelPendingEmailChanges(bool reset)
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await VerifiedAsync(factory, client, account);
        var target = AccountCredentials.New().Email; await BeginAsync(client, owner, account, target);
        var oldToken = factory.Email.TokenFor(target, AccountEmailPurpose.EmailChangeNew);
        if (reset)
        {
            using var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email });
            using var changed = await client.PostAsJsonAsync("/api/auth/reset-password", new { token = factory.Email.TokenFor(account.Email, AccountEmailPurpose.PasswordReset), password = AccountPasswordChangeTests.NewPassword });
            Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
            var notice = Assert.Single(factory.Email.Messages.Where(item => item.Purpose == AccountEmailPurpose.PasswordResetCompleted)); Assert.Empty(notice.Token);
        }
        else { using var changed = await AccountPasswordChangeTests.ChangeAsync(client, owner, account.Password); Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode); }
        using var confirm = await client.PostAsJsonAsync(Path + "/confirm-new", new { token = oldToken, confirmed = true }); Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.EmailChangeRequests.ToListAsync()); Assert.Equal(account.Email, (await db.Users.SingleAsync()).Email);
    }

    [Fact]
    public async Task IsolationReauthenticationCooldownAndSharedLockoutAreEnforced()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await VerifiedAsync(factory, client, account);
        var otherAccount = AccountCredentials.New(); var other = await VerifiedAsync(factory, client, otherAccount);
        var pending = await BeginAsync(client, owner, account, AccountCredentials.New().Email);
        Assert.Null(await PendingAsync(client, other.AccessToken));
        using var foreign = await PostAsync(client, "cancel", other.AccessToken, new { requestId = pending.Id, userId = owner.User.Id, confirmed = true }); Assert.Equal(HttpStatusCode.Conflict, foreign.StatusCode);
        using var tooSoon = await PostAsync(client, "request", owner.AccessToken, new { newEmail = AccountCredentials.New().Email, currentPassword = account.Password, confirmed = true }); Assert.Equal(HttpStatusCode.TooManyRequests, tooSoon.StatusCode);
        using var anon = await client.GetAsync(Path); Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);
        using var device = new HttpRequestMessage(HttpMethod.Get, Path); device.Headers.Authorization = new("Device", new string('A', 43));
        using var deviceDenied = await client.SendAsync(device); Assert.Equal(HttpStatusCode.Unauthorized, deviceDenied.StatusCode);
        for (var index = 0; index < factory.SecurityOptions.MaxFailedLoginAttempts; index++)
        {
            using var failed = await PostAsync(client, "request", other.AccessToken, new { newEmail = AccountCredentials.New().Email, currentPassword = "incorrect", confirmed = true }); Assert.Equal(HttpStatusCode.Forbidden, failed.StatusCode);
        }
        using var locked = await client.PostAsJsonAsync("/api/auth/login", new { otherAccount.Email, otherAccount.Password }); Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.NotNull(await PendingAsync(client, owner.AccessToken));
    }

    [Fact]
    public async Task PendingMetadataExportsWithoutHashesAndAccountDeletionCascades()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await VerifiedAsync(factory, client, account);
        var target = AccountCredentials.New().Email; await BeginAsync(client, owner, account, target);
        using var exportRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/account/export", owner.AccessToken);
        using var response = await client.SendAsync(exportRequest); var json = await response.Content.ReadAsStringAsync();
        Assert.Equal(target, JsonDocument.Parse(json).RootElement.GetProperty("pendingEmailChange").GetProperty("newEmail").GetString());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pending = await db.EmailChangeRequests.AsNoTracking().SingleAsync();
        foreach (var value in new[] { pending.CurrentTokenHash, pending.NewTokenHash, factory.Email.TokenFor(target, AccountEmailPurpose.EmailChangeNew) }) Assert.DoesNotContain(value, json);
        using var delete = AccountApiFactory.AuthorizedRequest(HttpMethod.Delete, "/api/account", owner.AccessToken);
        delete.Content = JsonContent.Create(new { password = account.Password, confirmation = "ELIMINAR" });
        using var deleted = await client.SendAsync(delete); Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await db.EmailChangeRequests.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ExpiredPendingMetadataUsesBoundedTechnicalRetention()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await VerifiedAsync(factory, client, account);
        await BeginAsync(client, owner, account, AccountCredentials.New().Email);
        factory.Clock.Advance(TimeSpan.FromDays(8));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = new RetentionMaintenanceService(db, Options.Create(new RetentionOptions { Enabled = true }), factory.Clock);
        var preview = await service.RunAsync(false, default); Assert.Equal(1, preview.Eligible!.EmailChanges);
        Assert.Single(await db.EmailChangeRequests.ToListAsync());
        var applied = await service.RunAsync(true, default); Assert.Equal(1, applied.Removed.EmailChanges); Assert.Empty(await db.EmailChangeRequests.ToListAsync());
        Assert.Single(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task FailureToStageRecoveryNoticeLeavesTheTokenAndSessionUsable()
    {
        using var factory = new AccountApiFactory(); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await VerifiedAsync(factory, client, account);
        using var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email });
        var token = factory.Email.TokenFor(account.Email, AccountEmailPurpose.PasswordReset);
        factory.Email.RejectedPurpose = AccountEmailPurpose.PasswordResetCompleted;
        using var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = AccountPasswordChangeTests.NewPassword });
        Assert.Equal(HttpStatusCode.InternalServerError, reset.StatusCode);
        using (var existing = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", owner.AccessToken))
        using (var active = await client.SendAsync(existing)) Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        factory.Email.RejectedPurpose = null;
        using var retry = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = AccountPasswordChangeTests.NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", owner.AccessToken);
        using var denied = await client.SendAsync(request); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        await AccountSessionManagementTests.LoginAsync(client, account with { Password = AccountPasswordChangeTests.NewPassword });
    }

    [Fact]
    public async Task UnverifiedAccountsAndRateLimitedRequestsCannotCreatePendingChanges()
    {
        using var factory = new AccountApiFactory(emailChangePermitLimit: 1); using var client = factory.CreateApiClient();
        var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
        using var notVerified = await PostAsync(client, "request", owner.AccessToken,
            new { newEmail = AccountCredentials.New().Email, currentPassword = account.Password, confirmed = true });
        Assert.Equal(HttpStatusCode.Forbidden, notVerified.StatusCode);
        using var limited = await PostAsync(client, "request", owner.AccessToken,
            new { newEmail = AccountCredentials.New().Email, currentPassword = account.Password, confirmed = true });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Null(await PendingAsync(client, owner.AccessToken));
        Assert.DoesNotContain(factory.Email.Messages, item => item.Purpose is AccountEmailPurpose.EmailChangeCurrent or AccountEmailPurpose.EmailChangeNew);
    }
}
