using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class RetentionPostgresFactAttribute : FactAttribute
{
    public RetentionPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SECURITY_CENTER_RETENTION_TEST_CONNECTION")))
            Skip = "Run scripts/test-retention.ps1 to provide an isolated temporary PostgreSQL database.";
    }
}

public sealed class RetentionPostgresTests
{
    [RetentionPostgresFact]
    public async Task ExpiredEmailChangeRequestsArePurgedByTheirRequestIdWithoutRemovingFreshRequestsOrOwners()
    {
        await using var db = Database(); await db.Database.EnsureCreatedAsync();
        var expiredOwner = Owner(); var freshOwner = Owner();
        var expired = new EmailChangeRequest { User = expiredOwner, UserId = expiredOwner.Id,
            CurrentEmail = expiredOwner.Email, NewEmail = $"expired-{Guid.NewGuid():N}@example.invalid",
            CurrentTokenHash = Hash(), NewTokenHash = Hash(), CreatedAtUtc = Now.AddDays(-9), ExpiresAtUtc = Now.AddDays(-8) };
        var fresh = new EmailChangeRequest { User = freshOwner, UserId = freshOwner.Id,
            CurrentEmail = freshOwner.Email, NewEmail = $"fresh-{Guid.NewGuid():N}@example.invalid",
            CurrentTokenHash = Hash(), NewTokenHash = Hash(), CreatedAtUtc = Now, ExpiresAtUtc = Now.AddMinutes(30) };
        db.EmailChangeRequests.AddRange(expired, fresh); await db.SaveChangesAsync();
        var preview = await Service(db, 1).RunAsync(false, default); Assert.Equal(1, preview.Eligible!.EmailChanges);
        Assert.Equal(2, await db.EmailChangeRequests.CountAsync());
        var applied = await Service(db, 1).RunAsync(true, default); Assert.Equal(1, applied.Removed.EmailChanges);
        Assert.Equal(fresh.Id, (await db.EmailChangeRequests.AsNoTracking().SingleAsync()).Id);
        Assert.True(await db.Users.AnyAsync(item => item.Id == expiredOwner.Id)); Assert.True(await db.Users.AnyAsync(item => item.Id == freshOwner.Id));
    }

    private static readonly DateTime Now = DateTime.UtcNow;
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
    private static string Connection()
    {
        var connection = Environment.GetEnvironmentVariable("SECURITY_CENTER_RETENTION_TEST_CONNECTION") ?? throw new InvalidOperationException("Missing isolated connection.");
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        if (!Regex.IsMatch(parsed.Database ?? "", "^scai_retention_test_[a-f0-9]{32}$") || parsed.Host != "127.0.0.1")
            throw new InvalidOperationException("Refused a database outside the isolated retention namespace.");
        return connection;
    }
    private static AppDbContext Database(IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Connection());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options);
    }
    private static RetentionMaintenanceService Service(AppDbContext db, int batch = 200) =>
        new(db, Options.Create(new RetentionOptions { Enabled = true, BatchSize = batch }), new Clock());
    private static User Owner() => new() { Name = "Isolated retention", Email = $"retention-{Guid.NewGuid():N}@example.invalid", PasswordHash = "NOT_A_REAL_PASSWORD_HASH" };
    private static string Hash() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    [RetentionPostgresFact]
    public async Task PreviewAndBatchesPreserveLiveReplayHashesAndAllProductContent()
    {
        await using var db = Database(); await db.Database.EnsureCreatedAsync();
        var user = Owner(); var device = new Device { User = user, Name = "Keep product content", CreatedAtUtc = Now.AddYears(-1) };
        var analysis = new SecurityAnalysis { User = user, Device = device, Score = 42, AnalyzedAtUtc = Now.AddYears(-1),
            Findings = [new SecurityFinding { RuleId = "WIN-TPM-READY", RuleVersion = 1, Title = "Keep", Severity = "informational", Evidence = "Keep", Recommendation = "Keep", DetectedAtUtc = Now.AddYears(-1) }] };
        db.SecurityAnalyses.Add(analysis);
        var feedback = new AnalysisFeedback { User = user, Analysis = analysis, Rating = "helpful", Comment = "Keep", CreatedAtUtc = Now.AddYears(-1), UpdatedAtUtc = Now.AddYears(-1), ExplanationVersion = 1 };
        db.AnalysisFeedback.Add(feedback);
        var marker = new DeletedAnalysisReport { Device = device, ReportId = Guid.NewGuid(), DeletedAtUtc = Now.AddYears(-1) };
        db.DeletedAnalysisReports.Add(marker);
        var live = new UserSession { User = user, CreatedAtUtc = Now.AddDays(-100), ExpiresAtUtc = Now.AddDays(10) };
        var expired = new UserSession { User = user, CreatedAtUtc = Now.AddDays(-100), ExpiresAtUtc = Now.AddDays(-31) };
        var revoked = new UserSession { User = user, CreatedAtUtc = Now.AddDays(-100), ExpiresAtUtc = Now.AddDays(10), RevokedAtUtc = Now.AddDays(-31) };
        db.UserSessions.AddRange(live, expired, revoked);
        foreach (var session in new[] { live, expired, revoked }) db.RefreshCredentials.Add(new RefreshCredential
        { Session = session, TokenHash = Hash(), CreatedAtUtc = Now.AddDays(-90), ConsumedAtUtc = Now.AddDays(-80), ExpiresAtUtc = session.ExpiresAtUtc });
        db.AccountActionTokens.AddRange(new AccountActionToken { User = user, Purpose = "password-reset", TokenHash = Hash(), CreatedAtUtc = Now.AddDays(-10), ExpiresAtUtc = Now.AddDays(-8) },
            new AccountActionToken { User = user, Purpose = "email-verification", TokenHash = Hash(), CreatedAtUtc = Now.AddDays(-10), ExpiresAtUtc = Now.AddDays(1), ConsumedAtUtc = Now.AddDays(-8) });
        for (var i = 0; i < 2; i++)
        {
            db.DevicePairingCodes.Add(new DevicePairingCode { Device = device, CodeHash = Hash(), CreatedAtUtc = Now.AddDays(-10), ExpiresAtUtc = Now.AddDays(-8) });
            db.DeviceCredentials.Add(new DeviceCredential { Device = device, TokenHash = Hash(), CreatedAtUtc = Now.AddDays(-100), ExpiresAtUtc = Now.AddDays(10), RevokedAtUtc = Now.AddDays(-31) });
        }
        db.DeviceCredentials.Add(new DeviceCredential { Device = device, TokenHash = Hash(), CreatedAtUtc = Now, ExpiresAtUtc = Now.AddDays(10) });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var service = Service(db, 1);
        var preview = await service.RunAsync(false, default);
        Assert.Equal(new RetentionCounts(2, 2, 2, 2), preview.Eligible);
        Assert.Equal(new RetentionCounts(0, 0, 0, 0), preview.Removed);
        Assert.Equal(3, await db.RefreshCredentials.CountAsync(item => item.Session.UserId == user.Id));
        Assert.Equal(new RetentionCounts(1, 1, 1, 1), (await service.RunAsync(true, default)).Removed);
        Assert.Equal(new RetentionCounts(1, 1, 1, 1), (await service.RunAsync(true, default)).Removed);
        Assert.Equal(new RetentionCounts(0, 0, 0, 0), (await service.RunAsync(true, default)).Removed);
        Assert.Equal(live.Id, (await db.UserSessions.SingleAsync(item => item.UserId == user.Id)).Id);
        Assert.Equal(live.Id, (await db.RefreshCredentials.SingleAsync(item => item.Session.UserId == user.Id)).UserSessionId);
        Assert.Equal(1, await db.DeviceCredentials.CountAsync(item => item.DeviceId == device.Id));
        Assert.Equal(42, (await db.SecurityAnalyses.SingleAsync(item => item.Id == analysis.Id)).Score);
        Assert.Equal("Keep", (await db.SecurityFindings.SingleAsync(item => item.SecurityAnalysisId == analysis.Id)).Evidence);
        Assert.True(await db.AnalysisFeedback.AnyAsync(item => item.Id == feedback.Id));
        Assert.True(await db.DeletedAnalysisReports.AnyAsync(item => item.DeviceId == device.Id && item.ReportId == marker.ReportId));
        Assert.True(await db.Users.AnyAsync(item => item.Id == user.Id)); Assert.True(await db.Devices.AnyAsync(item => item.Id == device.Id));
        Assert.Equal(10, await db.SecurityRules.CountAsync());
    }

    [RetentionPostgresFact]
    public async Task AdvisoryLockSkipsAnotherRunWithoutRemovingRows()
    {
        await using var held = Database(); await held.Database.EnsureCreatedAsync();
        var session = new UserSession { User = Owner(), CreatedAtUtc = Now.AddDays(-100), ExpiresAtUtc = Now.AddDays(-31) };
        held.UserSessions.Add(session); await held.SaveChangesAsync();
        await using var transaction = await held.Database.BeginTransactionAsync();
        const long lockKey = 0x534341495245544E;
        Assert.True(await held.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock({lockKey}) AS \"Value\"").SingleAsync());
        await using var other = Database();
        var report = await Service(other).RunAsync(true, default);
        Assert.Equal("busy", report.Status); Assert.Null(report.Eligible); Assert.Equal(0, report.Removed.Sessions);
        Assert.True(await other.UserSessions.AnyAsync(item => item.Id == session.Id));
        await transaction.RollbackAsync();
        Assert.Equal(1, (await Service(other).RunAsync(true, default)).Removed.Sessions);
    }

    [RetentionPostgresFact]
    public async Task DeleteRechecksEligibilityWhenASessionChangesAfterSelection()
    {
        Guid id;
        await using (var seed = Database())
        {
            await seed.Database.EnsureCreatedAsync();
            var session = new UserSession { User = Owner(), CreatedAtUtc = Now.AddDays(-100), ExpiresAtUtc = Now.AddDays(-31) };
            seed.UserSessions.Add(session); await seed.SaveChangesAsync(); id = session.Id;
        }
        await using var db = Database(new ExtendBeforeDelete(id));
        var report = await Service(db).RunAsync(true, default);
        Assert.Equal(1, report.Eligible!.Sessions); Assert.Equal(0, report.Removed.Sessions);
        Assert.True(await db.UserSessions.AnyAsync(item => item.Id == id && item.ExpiresAtUtc > Now));
    }

    private sealed class ExtendBeforeDelete(Guid id) : DbCommandInterceptor
    {
        private bool _changed;
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_changed && command.CommandText.Replace("\"", "").TrimStart().StartsWith("DELETE FROM user_sessions", StringComparison.OrdinalIgnoreCase))
            {
                _changed = true;
                await using var other = Database();
                await other.UserSessions.Where(item => item.Id == id).ExecuteUpdateAsync(set => set.SetProperty(item => item.ExpiresAtUtc, Now.AddDays(10)), cancellationToken);
            }
            return result;
        }
    }
}
