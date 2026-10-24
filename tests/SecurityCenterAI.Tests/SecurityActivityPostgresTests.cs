using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class AuditPostgresFactAttribute : FactAttribute
{
    public AuditPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SECURITY_CENTER_AUDIT_TEST_CONNECTION")))
            Skip = "Run scripts/test-security-activity.ps1 with an isolated fictitious PostgreSQL database.";
    }
}

public sealed class SecurityActivityPostgresTests
{
    [AuditPostgresFact]
    public async Task ConcurrentLinkMessageChecksCannotExceedTheDurableDailyQuota()
    {
        using var factory = new AccountApiFactory(postgresConnection: Environment.GetEnvironmentVariable("SECURITY_CENTER_AUDIT_TEST_CONNECTION"), linkMessageDailyLimit: 3);
        using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => LinkMessageTests.CheckAsync(client, owner.AccessToken)));
        try
        {
            Assert.Equal(3, responses.Count(item => item.StatusCode == HttpStatusCode.OK));
            Assert.Equal(5, responses.Count(item => item.StatusCode == HttpStatusCode.TooManyRequests));
            Assert.Equal(0, (await LinkMessageTests.QuotaAsync(client, owner.AccessToken)).Remaining);
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(3, (await db.LinkMessageUsage.SingleAsync(item => item.UserId == owner.User.Id)).Checks);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [AuditPostgresFact]
    public async Task FailedUsageWriteRollsBackAndAccountDeletionRemovesTheQuotaRow()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        var account = AccountCredentials.New(); var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, account);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stamp = (await db.Users.AsNoTracking().SingleAsync(item => item.Id == owner.User.Id)).ConcurrencyStamp;
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION public.scai_reject_tool_usage() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Simulated usage failure'; END; $$;
            CREATE TRIGGER scai_reject_tool_usage BEFORE INSERT ON public.link_message_usage
            FOR EACH ROW EXECUTE FUNCTION public.scai_reject_tool_usage();
            """);
        try
        {
            using var failed = await LinkMessageTests.CheckAsync(client, owner.AccessToken);
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.Equal(stamp, (await db.Users.AsNoTracking().SingleAsync(item => item.Id == owner.User.Id)).ConcurrencyStamp);
            Assert.False(await db.LinkMessageUsage.AnyAsync(item => item.UserId == owner.User.Id));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER scai_reject_tool_usage ON public.link_message_usage; DROP FUNCTION public.scai_reject_tool_usage();"); }
        using (var success = await LinkMessageTests.CheckAsync(client, owner.AccessToken)) Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        using var deletion = AccountApiFactory.AuthorizedRequest(HttpMethod.Delete, "/api/account", owner.AccessToken);
        deletion.Content = JsonContent.Create(new { account.Password, confirmation = "ELIMINAR" });
        using var deleted = await client.SendAsync(deletion); Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.False(await db.LinkMessageUsage.AnyAsync(item => item.UserId == owner.User.Id));
    }

    [AuditPostgresFact]
    public async Task FilteredActivityCursorUsesPostgresOrderingAndWholeUtcDayBoundaries()
    {
        using var factory = Factory(); await MigrateAsync(factory);
        await SecurityActivityTests.VerifyFilteredPaginationAsync(factory);
    }

    [AuditPostgresFact]
    public async Task EmailCompletionRacingOldAddressRecoveryCannotChangeThePasswordAfterHandover()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var account = AccountCredentials.New(); var owner = await AccountEmailChangeTests.VerifiedAsync(factory, client, account);
            var target = AccountCredentials.New().Email; var pending = await AccountEmailChangeTests.BeginAsync(client, owner, account, target);
            await AccountEmailChangeTests.ConfirmBothAsync(factory, client, account.Email, target);
            using var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email });
            var token = factory.Email.TokenFor(account.Email, SecurityCenterAI.Api.Services.AccountEmailPurpose.PasswordReset);
            var complete = AccountEmailChangeTests.PostAsync(client, "complete", owner.AccessToken,
                new { requestId = pending.Id, currentPassword = account.Password, confirmed = true });
            var recovery = client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = AccountPasswordChangeTests.NewPassword });
            await Task.WhenAll(complete, recovery); using var completed = await complete; using var reset = await recovery;
            Assert.Equal(1, new[] { completed, reset }.Count(item => item.IsSuccessStatusCode));
            Assert.Contains(completed.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.Unauthorized });
            Assert.Contains(reset.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.BadRequest });
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.EmailChangeRequests.AnyAsync(item => item.UserId == owner.User.Id));
            Assert.False(await db.UserSessions.AnyAsync(item => item.UserId == owner.User.Id && item.RevokedAtUtc == null));
            var login = account with { Email = completed.IsSuccessStatusCode ? target : account.Email,
                Password = completed.IsSuccessStatusCode ? account.Password : AccountPasswordChangeTests.NewPassword };
            await AccountSessionManagementTests.LoginAsync(client, login);
        }
    }

    [AuditPostgresFact]
    public async Task EmailChangeAndSessionRevocationRollBackWhenTheAuditInsertFails()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        var account = AccountCredentials.New(); var owner = await AccountEmailChangeTests.VerifiedAsync(factory, client, account);
        var target = AccountCredentials.New().Email;
        var pending = await AccountEmailChangeTests.BeginAsync(client, owner, account, target);
        await AccountEmailChangeTests.ConfirmBothAsync(factory, client, account.Email, target);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION public.scai_reject_email_activity() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Isolated email activity failure'; END; $$;
            CREATE TRIGGER scai_reject_email_activity BEFORE INSERT ON public.security_activity
            FOR EACH ROW WHEN (NEW.tipo_evento = 'EmailChanged') EXECUTE FUNCTION public.scai_reject_email_activity();
            """);
        try
        {
            using var failed = await AccountEmailChangeTests.PostAsync(client, "complete", owner.AccessToken,
                new { requestId = pending.Id, currentPassword = account.Password, confirmed = true });
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.Equal(account.Email, (await db.Users.AsNoTracking().SingleAsync(item => item.Id == owner.User.Id)).Email);
            Assert.True(await db.UserSessions.AnyAsync(item => item.UserId == owner.User.Id && item.RevokedAtUtc == null));
            Assert.True(await db.EmailChangeRequests.AnyAsync(item => item.UserId == owner.User.Id && item.CurrentConfirmedAtUtc != null && item.NewConfirmedAtUtc != null));
            Assert.DoesNotContain(factory.Email.Messages, item => item.Purpose == SecurityCenterAI.Api.Services.AccountEmailPurpose.EmailChanged);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER scai_reject_email_activity ON public.security_activity; DROP FUNCTION public.scai_reject_email_activity();"); }
        using var success = await AccountEmailChangeTests.PostAsync(client, "complete", owner.AccessToken,
            new { requestId = pending.Id, currentPassword = account.Password, confirmed = true });
        Assert.Equal(HttpStatusCode.NoContent, success.StatusCode);
    }

    [AuditPostgresFact]
    public async Task TwoApprovedOwnersCannotClaimTheSameEmailConcurrently()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var firstAccount = AccountCredentials.New(); var secondAccount = AccountCredentials.New();
            var first = await AccountEmailChangeTests.VerifiedAsync(factory, client, firstAccount);
            var second = await AccountEmailChangeTests.VerifiedAsync(factory, client, secondAccount);
            var target = AccountCredentials.New().Email;
            var firstPending = await AccountEmailChangeTests.BeginAsync(client, first, firstAccount, target);
            await AccountEmailChangeTests.ConfirmBothAsync(factory, client, firstAccount.Email, target);
            var secondPending = await AccountEmailChangeTests.BeginAsync(client, second, secondAccount, target);
            await AccountEmailChangeTests.ConfirmBothAsync(factory, client, secondAccount.Email, target);
            var results = await Task.WhenAll(AccountEmailChangeTests.PostAsync(client, "complete", first.AccessToken,
                new { requestId = firstPending.Id, currentPassword = firstAccount.Password, confirmed = true }),
                AccountEmailChangeTests.PostAsync(client, "complete", second.AccessToken,
                new { requestId = secondPending.Id, currentPassword = secondAccount.Password, confirmed = true }));
            try { Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }, results.Select(item => item.StatusCode).OrderBy(item => item).ToArray()); }
            finally { foreach (var response in results) response.Dispose(); }
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.Users.CountAsync(item => item.Email == target));
            Assert.Equal(1, await db.SecurityActivities.CountAsync(item => (item.UserId == first.User.Id || item.UserId == second.User.Id) && item.Kind == SecurityCenterAI.Domain.Entities.SecurityActivityKind.EmailChanged));
        }
    }

    [AuditPostgresFact]
    public async Task EmailCompletionRacingPasswordChangeHasOneSecurityOutcomeAndNoOldSessions()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var account = AccountCredentials.New(); var owner = await AccountEmailChangeTests.VerifiedAsync(factory, client, account);
            var target = AccountCredentials.New().Email; var pending = await AccountEmailChangeTests.BeginAsync(client, owner, account, target);
            await AccountEmailChangeTests.ConfirmBothAsync(factory, client, account.Email, target);
            var email = AccountEmailChangeTests.PostAsync(client, "complete", owner.AccessToken, new { requestId = pending.Id, currentPassword = account.Password, confirmed = true });
            var password = AccountPasswordChangeTests.ChangeAsync(client, owner, account.Password);
            await Task.WhenAll(email, password); using var emailResult = await email; using var passwordResult = await password;
            Assert.Equal(1, new[] { emailResult, passwordResult }.Count(item => item.IsSuccessStatusCode));
            Assert.All(new[] { emailResult, passwordResult }, item => Assert.Contains(item.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.Unauthorized }));
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.UserSessions.AnyAsync(item => item.UserId == owner.User.Id && item.RevokedAtUtc == null));
            Assert.False(await db.EmailChangeRequests.AnyAsync(item => item.UserId == owner.User.Id));
            Assert.Equal(emailResult.IsSuccessStatusCode ? target : account.Email, (await db.Users.AsNoTracking().SingleAsync(item => item.Id == owner.User.Id)).Email);
        }
    }

    [AuditPostgresFact]
    public async Task ConcurrentLoginsCannotExceedConfiguredSessionCapacity()
    {
        using var factory = new AccountApiFactory(maxActiveSessions: 3, postgresConnection:
            Environment.GetEnvironmentVariable("SECURITY_CENTER_AUDIT_TEST_CONNECTION"));
        using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
                client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password })));
            try
            {
                Assert.All(results, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
                Assert.Equal(2, results.Count(response => response.IsSuccessStatusCode));
                var list = await AccountSessionManagementTests.ReadAsync(client, owner.AccessToken);
                Assert.Equal(3, list.Sessions.Count); Assert.Equal(3, list.MaxActiveSessions);
                using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Equal(3, await db.UserSessions.CountAsync(item => item.UserId == owner.User.Id && item.RevokedAtUtc == null));
                Assert.Equal(2, await db.SecurityActivities.CountAsync(item => item.UserId == owner.User.Id &&
                    item.Kind == SecurityCenterAI.Domain.Entities.SecurityActivityKind.SessionStarted));
                Assert.Equal(3, await db.RefreshCredentials.CountAsync(item => item.Session.UserId == owner.User.Id));
            }
            finally { foreach (var response in results) response.Dispose(); }
        }
    }

    [AuditPostgresFact]
    public async Task ProfileRenameRacingPasswordChangeCannotRestoreOldCredentialsOrKeepSessionsActive()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        for (var index = 0; index < 3; index++)
        {
            var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
            var rename = AccountProfileNameTests.RenameAsync(client, owner.AccessToken, new { name = "Perfil actualizado" });
            var password = AccountPasswordChangeTests.ChangeAsync(client, owner, account.Password);
            await Task.WhenAll(rename, password);
            using var renamed = await rename; using var changed = await password;
            Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
            Assert.Contains(renamed.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.Unauthorized });
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.AsNoTracking().SingleAsync(item => item.Id == owner.User.Id);
            Assert.Equal(renamed.IsSuccessStatusCode ? "Perfil actualizado" : account.Name, user.Name);
            Assert.False(await db.UserSessions.AnyAsync(item => item.UserId == owner.User.Id && item.RevokedAtUtc == null));
            using var old = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password });
            Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
            var fresh = await AccountSessionManagementTests.LoginAsync(client, account with { Password = AccountPasswordChangeTests.NewPassword });
            Assert.Equal(user.Name, fresh.User.Name);
        }
    }

    [AuditPostgresFact]
    public async Task PasswordChangeRollsBackItsHashSessionsAndRecoveryTokensWhenTheActivityWriteFails()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
        using var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email });
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var original = await db.Users.AsNoTracking().SingleAsync(item => item.Id == owner.User.Id);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION public.scai_reject_password_activity() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Isolated password activity failure'; END; $$;
            CREATE TRIGGER scai_reject_password_activity BEFORE INSERT ON public.security_activity
            FOR EACH ROW WHEN (NEW.tipo_evento = 'PasswordChanged') EXECUTE FUNCTION public.scai_reject_password_activity();
            """);
        try
        {
            using var failed = await AccountPasswordChangeTests.ChangeAsync(client, owner, account.Password);
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.Equal(original.PasswordHash, (await db.Users.AsNoTracking().SingleAsync(item => item.Id == owner.User.Id)).PasswordHash);
            Assert.False(await db.UserSessions.AnyAsync(item => item.UserId == owner.User.Id && item.RevokedAtUtc != null));
            Assert.True(await db.AccountActionTokens.AnyAsync(item => item.UserId == owner.User.Id && item.Purpose == "password-reset" && item.ConsumedAtUtc == null));
            Assert.DoesNotContain(factory.Email.Messages, item => item.Purpose == SecurityCenterAI.Api.Services.AccountEmailPurpose.PasswordChanged);
        }
        finally
        { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER scai_reject_password_activity ON public.security_activity; DROP FUNCTION public.scai_reject_password_activity();"); }
        using var changed = await AccountPasswordChangeTests.ChangeAsync(client, owner, account.Password);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.True(await db.SecurityActivities.AnyAsync(item => item.UserId == owner.User.Id &&
            item.Kind == SecurityCenterAI.Domain.Entities.SecurityActivityKind.PasswordChanged));
    }

    [AuditPostgresFact]
    public async Task PasswordChangeRacingLoginAndRefreshCannotLeaveOldCredentialsWithActiveAccess()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        for (var index = 0; index < 3; index++)
        {
            var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
            var second = await AccountSessionManagementTests.LoginAsync(client, account);
            var change = AccountPasswordChangeTests.ChangeAsync(client, owner, account.Password);
            var login = client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password });
            var refresh = client.PostAsJsonAsync("/api/auth/refresh", new { second.RefreshToken });
            await Task.WhenAll(change, login, refresh);
            using var changed = await change; using var logged = await login; using var renewed = await refresh;
            Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
            foreach (var response in new[] { logged, renewed })
            {
                Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
                if (!response.IsSuccessStatusCode) continue;
                var auth = (await response.Content.ReadFromJsonAsync<AccountAuthentication>())!;
                using var me = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", auth.AccessToken);
                using var denied = await client.SendAsync(me); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
                using var blocked = await client.PostAsJsonAsync("/api/auth/refresh", new { auth.RefreshToken });
                Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);
            }
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.UserSessions.AnyAsync(item => item.UserId == owner.User.Id && item.RevokedAtUtc == null));
            Assert.Equal(1, await db.SecurityActivities.CountAsync(item => item.UserId == owner.User.Id &&
                item.Kind == SecurityCenterAI.Domain.Entities.SecurityActivityKind.PasswordChanged));
        }
    }

    [AuditPostgresFact]
    public async Task IndividualSessionRevocationUsesOwnerFilterAndRollsBackIfItsActivityCannotBeSaved()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
        var target = await AccountSessionManagementTests.LoginAsync(client, account);
        var foreign = await factory.RegisterAsync(client, AccountCredentials.New());
        var list = await AccountSessionManagementTests.ReadAsync(client, owner.AccessToken, "?userId=" + foreign.User.Id);
        Assert.Equal(2, list.Sessions.Count); Assert.True(list.Sessions[0].IsCurrent);
        Assert.DoesNotContain(list.Sessions, item => item.Id == AccountSessionManagementTests.SessionId(foreign));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION public.scai_reject_session_activity() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Isolated session activity failure'; END; $$;
            CREATE TRIGGER scai_reject_session_activity BEFORE INSERT ON public.security_activity
            FOR EACH ROW WHEN (NEW.tipo_evento = 'SessionRevoked') EXECUTE FUNCTION public.scai_reject_session_activity();
            """);
        try
        {
            using var failed = await AccountSessionManagementTests.RevokeAsync(client, owner.AccessToken, AccountSessionManagementTests.SessionId(target));
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.Null((await db.UserSessions.AsNoTracking().SingleAsync(item => item.Id == AccountSessionManagementTests.SessionId(target))).RevokedAtUtc);
            Assert.Equal(2, (await AccountSessionManagementTests.ReadAsync(client, owner.AccessToken)).Sessions.Count);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER scai_reject_session_activity ON public.security_activity; DROP FUNCTION public.scai_reject_session_activity();");
        }
        using var denied = await AccountSessionManagementTests.RevokeAsync(client, owner.AccessToken, AccountSessionManagementTests.SessionId(foreign));
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var success = await AccountSessionManagementTests.RevokeAsync(client, owner.AccessToken, AccountSessionManagementTests.SessionId(target));
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.Single((await AccountSessionManagementTests.ReadAsync(client, owner.AccessToken)).Sessions);
        Assert.Single((await AccountSessionManagementTests.ReadAsync(client, foreign.AccessToken)).Sessions);
    }

    [AuditPostgresFact]
    public async Task ConcurrentRefreshAndDuplicateRevocationCannotReactivateTheSelectedSession()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
            var target = await AccountSessionManagementTests.LoginAsync(client, account);
            var refresh = client.PostAsJsonAsync("/api/auth/refresh", new { target.RefreshToken });
            var first = AccountSessionManagementTests.RevokeAsync(client, owner.AccessToken, AccountSessionManagementTests.SessionId(target));
            var second = AccountSessionManagementTests.RevokeAsync(client, owner.AccessToken, AccountSessionManagementTests.SessionId(target));
            await Task.WhenAll(refresh, first, second);
            using var refreshed = await refresh; using var closed = await first; using var repeated = await second;
            Assert.Equal(HttpStatusCode.OK, closed.StatusCode); Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
            Assert.Contains(refreshed.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
            if (refreshed.IsSuccessStatusCode)
            {
                var rotated = (await refreshed.Content.ReadFromJsonAsync<AccountAuthentication>())!;
                using var me = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", rotated.AccessToken);
                using var blocked = await client.SendAsync(me); Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);
                using var renew = await client.PostAsJsonAsync("/api/auth/refresh", new { rotated.RefreshToken });
                Assert.Equal(HttpStatusCode.Unauthorized, renew.StatusCode);
            }
            Assert.Single((await AccountSessionManagementTests.ReadAsync(client, owner.AccessToken)).Sessions);
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.SecurityActivities.CountAsync(item => item.UserId == owner.User.Id &&
                item.Kind == SecurityCenterAI.Domain.Entities.SecurityActivityKind.SessionRevoked));
        }
    }

    private static AccountApiFactory Factory() => new(postgresConnection:
        Environment.GetEnvironmentVariable("SECURITY_CENTER_AUDIT_TEST_CONNECTION") ?? throw new InvalidOperationException("Missing isolated connection."));
    private static async Task MigrateAsync(AccountApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    [AuditPostgresFact]
    public async Task AnAuditInsertFailureRollsBackTheBusinessOperationAsWell()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = AccountCredentials.New();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION public.scai_reject_activity() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Isolated audit failure'; END; $$;
            CREATE TRIGGER scai_reject_activity BEFORE INSERT ON public.security_activity
            FOR EACH ROW EXECUTE FUNCTION public.scai_reject_activity();
            """);
        try
        {
            using var response = await client.PostAsJsonAsync("/api/auth/register", new { account.Name, account.Email, account.Password });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.False(await db.Users.AnyAsync(item => item.Email == account.Email));
            Assert.DoesNotContain(account.Password, await response.Content.ReadAsStringAsync());
            Assert.Empty(factory.Email.Messages);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER scai_reject_activity ON public.security_activity; DROP FUNCTION public.scai_reject_activity();");
        }
        var owner = await factory.RegisterAsync(client, account);
        Assert.Single(await db.SecurityActivities.Where(item => item.UserId == owner.User.Id).ToListAsync());
        Assert.Single(await db.UserSessions.Where(item => item.UserId == owner.User.Id).ToListAsync());
    }

    [AuditPostgresFact]
    public async Task PrivateEventsUseRealCascadesAndKeepOtherOwnersUntouched()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        var account = AccountCredentials.New(); var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, account);
        var foreign = await factory.RegisterAsync(client, AccountCredentials.New());
        var device = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        await DeviceApiTestSupport.PairAsync(client, device.PairingCode);
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/account/security-activity?userId=" + foreign.User.Id, owner.AccessToken);
        using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var activity = (await response.Content.ReadFromJsonAsync<SecurityActivityListResponse>())!;
        Assert.Equal(4, activity.Events.Count);
        using var delete = AccountApiFactory.AuthorizedRequest(HttpMethod.Delete, "/api/account", owner.AccessToken);
        delete.Content = JsonContent.Create(new { account.Password, confirmation = "ELIMINAR" });
        using var deleted = await client.SendAsync(delete); Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.SecurityActivities.AnyAsync(item => item.UserId == owner.User.Id));
        Assert.True(await db.SecurityActivities.AnyAsync(item => item.UserId == foreign.User.Id));
    }

    [AuditPostgresFact]
    public async Task LoginAndDeletionRacesCannotLeaveAuditRowsOrReactivateTheOwner()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var account = AccountCredentials.New(); var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, account);
            var device = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
            using var delete = AccountApiFactory.AuthorizedRequest(HttpMethod.Delete, "/api/account", owner.AccessToken);
            delete.Content = JsonContent.Create(new { account.Password, confirmation = "ELIMINAR" });
            var login = client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password });
            using var revoke = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, $"/api/devices/{device.Device.Id}/revoke", owner.AccessToken);
            var revocation = client.SendAsync(revoke);
            var deletion = client.SendAsync(delete);
            await Task.WhenAll(login, deletion, revocation);
            using var loginResponse = await login; using var deleted = await deletion;
            using var revoked = await revocation;
            Assert.Contains(revoked.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound });
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            Assert.Contains(loginResponse.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Users.AnyAsync(item => item.Id == owner.User.Id));
            Assert.False(await db.SecurityActivities.AnyAsync(item => item.UserId == owner.User.Id));
        }
    }
}
