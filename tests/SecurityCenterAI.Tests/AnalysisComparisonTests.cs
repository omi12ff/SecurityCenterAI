using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Domain.Rules;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class AnalysisComparisonTests
{
    [Theory]
    [InlineData("passed", "passed", "unchanged")]
    [InlineData("attention", "attention", "unchanged")]
    [InlineData("unknown", "unknown", "unchanged")]
    [InlineData("notApplicable", "notApplicable", "unchanged")]
    [InlineData("attention", "passed", "improved")]
    [InlineData("passed", "attention", "needsReview")]
    [InlineData("unknown", "passed", "newInformation")]
    [InlineData("unknown", "attention", "newInformation")]
    [InlineData("passed", "unknown", "lostInformation")]
    [InlineData("attention", "unknown", "lostInformation")]
    [InlineData("notApplicable", "passed", "notComparable")]
    [InlineData("notApplicable", "attention", "notComparable")]
    [InlineData("notApplicable", "unknown", "notComparable")]
    [InlineData("passed", "notApplicable", "notComparable")]
    [InlineData("attention", "notApplicable", "notComparable")]
    [InlineData("unknown", "notApplicable", "notComparable")]
    public async Task StatusTransitions_DistinguishObservationFromInformation(string baselineStatus,
        string targetStatus, string expectedChange)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var seeded = await SeedAsync(factory, client, baselineStatus, targetStatus);
        var result = await CompareAsync(client, seeded);
        var changed = Assert.Single(result.Checks, item => item.RuleId == FirstRule);
        Assert.Equal(expectedChange, changed.Change);
        Assert.Equal(baselineStatus, changed.BaselineStatus);
        Assert.Equal(targetStatus, changed.TargetStatus);
        Assert.Equal(10, SummaryCount(result.Summary));
        Assert.Equal(expectedChange == "improved" ? 1 : 0, result.Summary.Improved);
        Assert.Equal(expectedChange == "newInformation" ? 1 : 0, result.Summary.NewInformation);
        Assert.Equal(expectedChange == "lostInformation" ? 1 : 0, result.Summary.LostInformation);
        Assert.Equal(expectedChange == "needsReview" ? 1 : 0, result.Summary.NeedsReview);
        Assert.Equal(expectedChange == "notComparable" ? 1 : 0, result.Summary.NotComparable);
        if (baselineStatus == "unknown" && targetStatus == "unknown")
            Assert.Contains("sigue siendo desconocido", changed.Explanation);
        if (baselineStatus == "notApplicable" ^ targetStatus == "notApplicable")
        {
            Assert.False(result.ScoreComparable);
            Assert.Null(result.ScoreDelta);
            Assert.Null(result.CoverageDelta);
        }
    }

    [Theory]
    [InlineData("source")]
    [InlineData("schema")]
    [InlineData("scoring")]
    [InlineData("ruleVersion")]
    [InlineData("status")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("unknownRule")]
    [InlineData("score")]
    public async Task UnsupportedOrIncompleteHistory_DoesNotInventComparablePoints(string unsupported)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var seeded = await SeedAsync(factory, client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var target = await db.SecurityAnalyses.Include(item => item.Findings).SingleAsync(item => item.Id == seeded.TargetId);
            var finding = target.Findings.Single(item => item.RuleId == FirstRule);
            switch (unsupported)
            {
                case "source": target.Source = null; break;
                case "schema": target.SchemaVersion = 1; break;
                case "scoring": target.ScoringVersion = 2; break;
                case "ruleVersion": finding.RuleVersion = 2; break;
                case "status": finding.Status = null; break;
                case "missing": db.SecurityFindings.Remove(finding); break;
                case "duplicate":
                case "unknownRule":
                    var additional = Finding(unsupported == "duplicate" ? finding.RuleId : "UNRECOGNIZED-RULE", "passed");
                    additional.SecurityAnalysisId = target.Id;
                    db.SecurityFindings.Add(additional);
                    break;
                case "score": target.Score = null; break;
            }
            await db.SaveChangesAsync();
        }
        var result = await CompareAsync(client, seeded);
        Assert.False(result.ScoreComparable);
        Assert.Null(result.ScoreDelta);
        Assert.Null(result.CoverageDelta);
        if (unsupported is "source" or "schema" or "scoring") Assert.Equal(10, result.Summary.NotComparable);
        if (unsupported is "ruleVersion" or "status" or "missing" or "duplicate")
        {
            Assert.Equal("notComparable", result.Checks.Single(item => item.RuleId == FirstRule).Change);
            Assert.Equal(1, result.Summary.NotComparable);
        }
        if (unsupported == "unknownRule")
        {
            Assert.Equal(11, result.Checks.Count);
            var unknown = result.Checks.Single(item => item.RuleId == "UNRECOGNIZED-RULE");
            Assert.Null(unknown.BaselineFindingId);
            Assert.Equal("notComparable", unknown.Change);
        }
    }

    [Fact]
    public async Task Comparison_PreservesStoredReportsAndComputesDeltasWithoutInferringRemediation()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var seeded = await SeedAsync(factory, client, "attention", "passed");
        string before;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var baseline = await db.SecurityAnalyses.Include(item => item.Findings).SingleAsync(item => item.Id == seeded.BaselineId);
            var target = await db.SecurityAnalyses.Include(item => item.Findings).SingleAsync(item => item.Id == seeded.TargetId);
            baseline.Findings.Single(item => item.RuleId == "WIN-TPM-READY").Status = "unknown";
            target.Findings.Single(item => item.RuleId == "WIN-UAC-ENABLED").Status = "unknown";
            baseline.Score = 80;
            target.Score = 90;
            baseline.CoveragePercent = target.CoveragePercent = 90;
            await db.SaveChangesAsync();
            before = await SnapshotAsync(db);
        }
        var result = await CompareAsync(client, seeded);
        Assert.True(result.ScoreComparable);
        Assert.Equal(10, result.ScoreDelta);
        Assert.Equal(0, result.CoverageDelta);
        Assert.Equal(new AnalysisComparisonSummary(1, 0, 1, 1, 7, 0), result.Summary);
        Assert.Equal(80, result.Baseline.Score);
        Assert.Equal(90, result.Target.Score);
        var finding = result.Baseline.Findings.Single(item => item.RuleId == FirstRule);
        Assert.Equal("<script>Original evidence</script>", finding.Evidence);
        Assert.Equal("Original recommendation", finding.Recommendation);
        Assert.DoesNotContain("<script>", result.Checks.Single(item => item.RuleId == FirstRule).Explanation);
        using var finalScope = factory.Services.CreateScope();
        Assert.Equal(before, await SnapshotAsync(finalScope.ServiceProvider.GetRequiredService<AppDbContext>()));
    }

    [Fact]
    public async Task Ownership_RequiresMatchingUserAndDeviceForBothReports()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var seeded = await SeedAsync(factory, client);
        var foreign = await SeedAsync(factory, client);
        foreach (var path in new[]
        {
            Path(foreign.DeviceId, seeded.BaselineId, seeded.TargetId),
            Path(seeded.DeviceId, seeded.BaselineId, foreign.TargetId),
            Path(seeded.DeviceId, foreign.BaselineId, seeded.TargetId),
            Path(Guid.NewGuid(), seeded.BaselineId, seeded.TargetId),
            Path(seeded.DeviceId, seeded.BaselineId, Guid.NewGuid())
        })
        {
            using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, path, seeded.Owner.AccessToken);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using var requestForeign = AccountApiFactory.AuthorizedRequest(HttpMethod.Get,
            Path(seeded.DeviceId, seeded.BaselineId, seeded.TargetId), foreign.Owner.AccessToken);
        using var deniedForeign = await client.SendAsync(requestForeign);
        Assert.Equal(HttpStatusCode.NotFound, deniedForeign.StatusCode);
    }

    [Fact]
    public async Task Authentication_RequiresUserBearerAndRetainsRevokedDeviceHistory()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var seeded = await SeedAsync(factory, client);
        var path = Path(seeded.DeviceId, seeded.BaselineId, seeded.TargetId);
        using var anonymous = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var credential = await DeviceApiTestSupport.PairAsync(client, seeded.PairingCode);
        foreach (var (scheme, token) in new[] { ("Device", credential.DeviceToken), ("Bearer", credential.DeviceToken), ("Device", seeded.Owner.AccessToken) })
        {
            using var deviceRequest = new HttpRequestMessage(HttpMethod.Get, path);
            deviceRequest.Headers.Authorization = new AuthenticationHeaderValue(scheme, token);
            using var denied = await client.SendAsync(deviceRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        using var revoke = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, $"/api/devices/{seeded.DeviceId}/revoke", seeded.Owner.AccessToken);
        using var revoked = await client.SendAsync(revoke);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        var comparison = await CompareAsync(client, seeded);
        Assert.Equal(seeded.DeviceId, comparison.DeviceId);
    }

    [Fact]
    public async Task Selection_RejectsSameOrReverseReportsAndAllowsEqualReceptionTimes()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var seeded = await SeedAsync(factory, client);
        foreach (var (baseline, target) in new[]
        {
            (seeded.BaselineId, seeded.BaselineId), (seeded.TargetId, seeded.BaselineId),
            (Guid.Empty, seeded.TargetId)
        })
        {
            using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, Path(seeded.DeviceId, baseline, target), seeded.Owner.AccessToken);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var reports = await db.SecurityAnalyses.ToListAsync();
            reports.ForEach(item => item.AnalyzedAtUtc = factory.Clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
        }
        Assert.True((await CompareAsync(client, seeded)).ScoreComparable);
    }

    [Fact]
    public async Task Selection_RejectsReportsReceivedInOrderButObservedInReverseOrder()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var seeded = await SeedAsync(factory, client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var baseline = await db.SecurityAnalyses.SingleAsync(item => item.Id == seeded.BaselineId);
            var target = await db.SecurityAnalyses.SingleAsync(item => item.Id == seeded.TargetId);
            target.CompletedAtUtc = baseline.CompletedAtUtc!.Value.AddDays(-1);
            await db.SaveChangesAsync();
        }
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get,
            Path(seeded.DeviceId, seeded.BaselineId, seeded.TargetId), seeded.Owner.AccessToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync();
        Assert.Contains("orden inverso", problem);
    }

    [Fact]
    public async Task Comparison_UsesDeviceAccountRateLimit()
    {
        using var factory = new AccountApiFactory(devicePermitLimit: 2);
        using var client = factory.CreateApiClient();
        var seeded = await SeedAsync(factory, client);
        await CompareAsync(client, seeded);
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get,
            Path(seeded.DeviceId, seeded.BaselineId, seeded.TargetId), seeded.Owner.AccessToken);
        using var limited = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    private const string FirstRule = "WIN-DEFENDER-ANTIVIRUS";

    private sealed record SeededComparison(AccountAuthentication Owner, Guid DeviceId,
        Guid BaselineId, Guid TargetId, string PairingCode);

    private static async Task<SeededComparison> SeedAsync(AccountApiFactory factory, HttpClient client,
        string baselineStatus = "passed", string targetStatus = "passed")
    {
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var pairing = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        SecurityAnalysis Analysis(string changedStatus, DateTime receivedAt)
        {
            var findings = WindowsRuleCatalog.Rules.Select(rule => Finding(rule.Id, rule.Id == FirstRule ? changedStatus : "passed")).ToList();
            var applicable = findings.Where(item => item.Status != "notApplicable").ToArray();
            var denominator = WindowsRuleCatalog.Rules.Where(rule => applicable.Any(item => item.RuleId == rule.Id)).Sum(rule => rule.Weight);
            var earned = WindowsRuleCatalog.Rules.Where(rule => findings.Any(item => item.RuleId == rule.Id && item.Status == "passed")).Sum(rule => rule.Weight);
            return new SecurityAnalysis
            {
                UserId = owner.User.Id, DeviceId = pairing.Device.Id, Source = "windows-agent", SchemaVersion = 2,
                ScoringVersion = 1, Score = 100 * earned / denominator, AnalyzedAtUtc = receivedAt,
                CoveragePercent = 100 * applicable.Count(item => item.Status is "passed" or "attention") / applicable.Length,
                PassedCount = findings.Count(item => item.Status == "passed"), AttentionCount = findings.Count(item => item.Status == "attention"),
                UnknownCount = findings.Count(item => item.Status == "unknown"), NotApplicableCount = findings.Count(item => item.Status == "notApplicable"),
                Findings = findings, ReportId = Guid.NewGuid(), AgentVersion = "0.4.0-test", StartedAtUtc = receivedAt.AddSeconds(-10), CompletedAtUtc = receivedAt
            };
        }
        var baseline = Analysis(baselineStatus, now.AddMinutes(-1));
        var target = Analysis(targetStatus, now);
        db.SecurityAnalyses.AddRange(baseline, target);
        await db.SaveChangesAsync();
        return new(owner, pairing.Device.Id, baseline.Id, target.Id, pairing.PairingCode);
    }

    private static SecurityFinding Finding(string ruleId, string status) => new()
    {
        RuleId = ruleId, RuleVersion = 1, Title = "Stored finding", Severity = "informational", Status = status,
        Evidence = "<script>Original evidence</script>", Recommendation = "Original recommendation"
    };

    private static string Path(Guid deviceId, Guid baselineId, Guid targetId) =>
        $"/api/devices/{deviceId}/analyses/compare?baselineId={baselineId}&targetId={targetId}";

    private static async Task<AnalysisComparisonResponse> CompareAsync(HttpClient client, SeededComparison seeded)
    {
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get,
            Path(seeded.DeviceId, seeded.BaselineId, seeded.TargetId), seeded.Owner.AccessToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AnalysisComparisonResponse>();
        Assert.NotNull(result);
        return result;
    }

    private static int SummaryCount(AnalysisComparisonSummary summary) => summary.Improved + summary.NeedsReview +
        summary.NewInformation + summary.LostInformation + summary.Unchanged + summary.NotComparable;

    private static async Task<string> SnapshotAsync(AppDbContext db) => JsonSerializer.Serialize(await db.SecurityAnalyses
        .AsNoTracking().OrderBy(item => item.Id).Select(item => new
        {
            item.Id, item.Score, item.CoveragePercent, item.AnalyzedAtUtc, item.Source, item.ScoringVersion,
            Findings = item.Findings.OrderBy(finding => finding.Id).Select(finding => new
            { finding.Id, finding.RuleId, finding.RuleVersion, finding.Status, finding.Evidence, finding.Recommendation }).ToArray()
        }).ToArrayAsync());
}
