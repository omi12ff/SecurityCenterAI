using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class RetentionMaintenanceTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static RetentionMaintenanceService Service(AppDbContext db, bool enabled = true, int batch = 200) =>
        new(db, Options.Create(new RetentionOptions { Enabled = enabled, BatchSize = batch }), new FixedClock());
    private static User User() => new() { Name = "Retention fixture", Email = "retention@example.invalid", PasswordHash = "PRIVATE_HASH" };
    private static UserSession Session(User user, DateTime expires, DateTime? revoked = null) => new()
    { User = user, UserId = user.Id, CreatedAtUtc = Now.AddDays(-100), ExpiresAtUtc = expires, RevokedAtUtc = revoked };
    private static RefreshCredential Refresh(UserSession session) => new()
    { Session = session, UserSessionId = session.Id, TokenHash = "PRIVATE_REFRESH_HASH", ExpiresAtUtc = session.ExpiresAtUtc, CreatedAtUtc = Now.AddDays(-90), ConsumedAtUtc = Now.AddDays(-80) };

    [Fact]
    public async Task PreviewWorksWhileDisabledAndDoesNotChangeEligibleRowsOrExposeIdentities()
    {
        using var db = Database();
        var user = User();
        var session = Session(user, Now.AddDays(-31));
        db.UserSessions.Add(session); db.RefreshCredentials.Add(Refresh(session)); await db.SaveChangesAsync();
        var report = await Service(db, false).RunAsync(false, default);
        Assert.Equal("preview", report.Status); Assert.Equal(1, report.Eligible!.Sessions);
        Assert.Equal(new RetentionCounts(0, 0, 0, 0), report.Removed);
        Assert.Single(await db.UserSessions.ToListAsync()); Assert.Single(await db.RefreshCredentials.ToListAsync());
        var json = System.Text.Json.JsonSerializer.Serialize(report);
        Assert.DoesNotContain(user.Email, json); Assert.DoesNotContain("PRIVATE", json); Assert.DoesNotContain(user.Id.ToString(), json);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, false).RunAsync(true, default));
        Assert.Single(await db.UserSessions.ToListAsync());
    }

    [Fact]
    public async Task PurgeCascadesClosedSessionsButPreservesEveryRefreshOfALiveSession()
    {
        using var db = Database(); var user = User();
        var expired = Session(user, Now.AddDays(-31));
        var revoked = Session(user, Now.AddDays(100), Now.AddDays(-31));
        var live = Session(user, Now.AddDays(100));
        var recentlyExpired = Session(user, Now.AddDays(-1));
        db.UserSessions.AddRange(expired, revoked, live, recentlyExpired);
        foreach (var session in new[] { expired, revoked, live, recentlyExpired }) db.RefreshCredentials.Add(Refresh(session));
        await db.SaveChangesAsync();
        var result = await Service(db).RunAsync(true, default);
        Assert.Equal(2, result.Removed.Sessions);
        var retained = await db.UserSessions.Select(item => item.Id).ToArrayAsync();
        Assert.Contains(live.Id, retained); Assert.Contains(recentlyExpired.Id, retained); Assert.Equal(2, retained.Length);
        Assert.Equal(2, await db.RefreshCredentials.CountAsync());
        Assert.True(await db.RefreshCredentials.AnyAsync(item => item.UserSessionId == live.Id && item.ConsumedAtUtc < Now.AddDays(-30)));
        Assert.Single(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task CutoffIsInclusiveAndRecentOrActiveArtifactsRemain()
    {
        using var db = Database(); var user = User();
        var device = new Device { User = user, Name = "Keep device", CreatedAtUtc = Now.AddDays(-100) };
        db.Devices.Add(device);
        db.UserSessions.AddRange(Session(user, Now.AddDays(-30)), Session(user, Now.AddDays(-30).AddTicks(1)));
        db.AccountActionTokens.AddRange(
            new() { User = user, Purpose = "password-reset", TokenHash = "old", ExpiresAtUtc = Now.AddDays(-7) },
            new() { User = user, Purpose = "email-verification", TokenHash = "consumed", ExpiresAtUtc = Now.AddDays(10), ConsumedAtUtc = Now.AddDays(-7) },
            new() { User = user, Purpose = "password-reset", TokenHash = "recent", ExpiresAtUtc = Now.AddDays(-7).AddTicks(1) });
        db.DevicePairingCodes.AddRange(
            new() { Device = device, CodeHash = "old-code", ExpiresAtUtc = Now.AddDays(10), RevokedAtUtc = Now.AddDays(-7) },
            new() { Device = device, CodeHash = "valid-code", ExpiresAtUtc = Now.AddMinutes(5) });
        db.DeviceCredentials.AddRange(
            new() { Device = device, TokenHash = "old-credential", ExpiresAtUtc = Now.AddDays(30), RevokedAtUtc = Now.AddDays(-30) },
            new() { Device = device, TokenHash = "valid-credential", ExpiresAtUtc = Now.AddDays(30) });
        await db.SaveChangesAsync();
        var result = await Service(db).RunAsync(true, default);
        Assert.Equal(new RetentionCounts(1, 2, 1, 1), result.Removed);
        Assert.Single(await db.UserSessions.ToListAsync()); Assert.Single(await db.AccountActionTokens.ToListAsync());
        Assert.Equal("valid-code", (await db.DevicePairingCodes.SingleAsync()).CodeHash);
        Assert.Equal("valid-credential", (await db.DeviceCredentials.SingleAsync()).TokenHash);
        Assert.Equal(device.Id, (await db.Devices.SingleAsync()).Id);
    }

    [Fact]
    public async Task EachCategoryIsBoundedAndRepeatRunsAreIdempotentAfterTheBacklogIsDrained()
    {
        using var db = Database(); var user = User();
        for (var index = 0; index < 5; index++) db.UserSessions.Add(Session(user, Now.AddDays(-31 - index)));
        await db.SaveChangesAsync(); var service = Service(db, batch: 2);
        var first = await service.RunAsync(true, default);
        Assert.Equal(5, first.Eligible!.Sessions); Assert.Equal(2, first.Removed.Sessions);
        Assert.Equal(2, (await service.RunAsync(true, default)).Removed.Sessions);
        Assert.Equal(1, (await service.RunAsync(true, default)).Removed.Sessions);
        Assert.Equal(0, (await service.RunAsync(true, default)).Removed.Sessions);
        Assert.Single(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task ProductHistoryAndDeletionMarkersAreNeverRetentionCandidates()
    {
        using var db = Database(); var user = User();
        var device = new Device { User = user, Name = "Keep revoked device", RevokedAtUtc = Now.AddYears(-2) };
        var analysis = new SecurityAnalysis { User = user, Device = device, Score = 42, AnalyzedAtUtc = Now.AddYears(-2) };
        analysis.Findings.Add(new SecurityFinding { RuleId = "rule", RuleVersion = 1, Title = "Keep", Severity = "low", Evidence = "Keep", Recommendation = "Keep", DetectedAtUtc = Now.AddYears(-2) });
        db.SecurityAnalyses.Add(analysis);
        db.AnalysisFeedback.Add(new AnalysisFeedback { User = user, Analysis = analysis, Rating = "helpful", Comment = "Keep" });
        db.DeletedAnalysisReports.Add(new DeletedAnalysisReport { Device = device, ReportId = Guid.NewGuid(), DeletedAtUtc = Now.AddYears(-2) });
        db.SecurityActivities.Add(new SecurityActivity { User = user, UserId = user.Id, Kind = SecurityActivityKind.AccountRegistered, OccurredAtUtc = Now.AddYears(-2) });
        await db.SaveChangesAsync();
        Assert.Equal(new RetentionCounts(0, 0, 0, 0), (await Service(db).RunAsync(true, default)).Removed);
        Assert.Single(await db.Users.ToListAsync()); Assert.Single(await db.Devices.ToListAsync()); Assert.Single(await db.SecurityAnalyses.ToListAsync());
        Assert.Single(await db.SecurityFindings.ToListAsync()); Assert.Single(await db.AnalysisFeedback.ToListAsync()); Assert.Single(await db.DeletedAnalysisReports.ToListAsync());
        Assert.Single(await db.SecurityActivities.ToListAsync());
    }

    [Theory]
    [InlineData(0, 200, 24)]
    [InlineData(6, 200, 24)]
    [InlineData(30, 0, 24)]
    [InlineData(30, 1001, 24)]
    [InlineData(30, 200, 0)]
    [InlineData(30, 200, 169)]
    public void UnsafeConfigurationIsRejected(int days, int batch, int hours) =>
        Assert.False(RetentionOptions.Valid(new RetentionOptions { ClosedSessionDays = days, BatchSize = batch, IntervalHours = hours }));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkerDoesNotPurgeOnStartupAndStopsWithoutOpeningADatabaseScope(bool enabled)
    {
        using var worker = new RetentionWorker(new NoScopes(), Options.Create(new RetentionOptions { Enabled = enabled }), new FixedClock(), NullLogger<RetentionWorker>.Instance);
        await worker.StartAsync(default); await worker.StopAsync(default);
    }

    [Fact]
    public async Task CancellationDoesNotBecomeASuccessReport()
    {
        using var db = Database(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(db).RunAsync(true, cancellation.Token));
    }

    private sealed class NoScopes : IServiceScopeFactory
    { public IServiceScope CreateScope() => throw new InvalidOperationException("Disabled retention must not access storage."); }
}
