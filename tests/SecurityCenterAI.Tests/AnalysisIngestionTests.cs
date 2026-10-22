using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Domain.Rules;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class AnalysisIngestionTests
{
    private static SubmitAnalysisRequest Report(AccountApiFactory factory, string status = "passed")
    {
        var completed = factory.Clock.GetUtcNow();
        return new SubmitAnalysisRequest(Guid.NewGuid(), 2, "0.2.0-test", completed.AddSeconds(-10), completed,
            WindowsRuleCatalog.Rules.Select(rule => new SubmittedCheck(rule.Id, 1, status,
                "Evidencia declarada por el agente de prueba.", completed.AddSeconds(-1))).ToArray());
    }

    private static async Task<(AccountAuthentication Owner, TestDeviceCredential Credential)> PairAsync(AccountApiFactory factory, HttpClient client)
    {
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        return (owner, await DeviceApiTestSupport.PairAsync(client, code.PairingCode));
    }

    private static HttpRequestMessage Submit(object body, string? token = null, string scheme = "Device")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/agent/analyses") { Content = JsonContent.Create(body) };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue(scheme, token);
        return request;
    }

    [Fact]
    public async Task Submission_UsesAuthenticatedOwnershipAndServerCatalogAndReceiptTime()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var (owner, credential) = await PairAsync(factory, client);
        var report = Report(factory);
        using var request = Submit(report, credential.DeviceToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var receipt = await response.Content.ReadFromJsonAsync<AnalysisReceipt>();
        Assert.NotNull(receipt);
        Assert.Equal(100, receipt.Score);
        Assert.Equal(100, receipt.CoveragePercent);
        Assert.Equal(10, receipt.PassedCount);
        Assert.Equal(0, receipt.AttentionCount);
        Assert.False(receipt.AlreadyReceived);
        Assert.Equal(factory.Clock.GetUtcNow().UtcDateTime, receipt.ReceivedAtUtc);
        Assert.Equal(report.ReportId, receipt.ReportId);

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var analysis = await database.SecurityAnalyses.Include(item => item.Findings).SingleAsync();
        Assert.Equal(owner.User.Id, analysis.UserId);
        Assert.Equal(credential.DeviceId, analysis.DeviceId);
        Assert.Equal("windows-agent", analysis.Source);
        Assert.Equal(report.StartedAtUtc.UtcDateTime, analysis.StartedAtUtc);
        Assert.Equal(report.CompletedAtUtc.UtcDateTime, analysis.CompletedAtUtc);
        Assert.Equal(10, analysis.Findings.Count);
        foreach (var finding in analysis.Findings)
        {
            var rule = WindowsRuleCatalog.Rules.Single(item => item.Id == finding.RuleId);
            Assert.Equal(rule.Title, finding.Title);
            Assert.Equal(rule.Recommendation, finding.Recommendation);
            Assert.Equal("informational", finding.Severity);
            Assert.Equal("passed", finding.Status);
        }
        Assert.Equal("Windows", (await database.Devices.SingleAsync()).Platform);
        Assert.Equal(receipt.ReceivedAtUtc, (await database.Devices.SingleAsync()).LastSeenAtUtc);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(credential.DeviceToken, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("hash", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknowns_ReceiveNoPointsAndAreInformationalRatherThanAlerts()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var (_, credential) = await PairAsync(factory, client);
        var unknownIds = new[] { "WIN-SECURE-BOOT", "WIN-TPM-READY", "WIN-BITLOCKER-SYSTEM" };
        var report = Report(factory);
        report = report with { Checks = report.Checks.Select(check => unknownIds.Contains(check.RuleId)
            ? check with { Status = "unknown" } : check).ToArray() };
        using var request = Submit(report, credential.DeviceToken);
        using var response = await client.SendAsync(request);
        var receipt = await response.Content.ReadFromJsonAsync<AnalysisReceipt>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(receipt);
        Assert.Equal(75, receipt.Score);
        Assert.Equal(70, receipt.CoveragePercent);
        Assert.Equal(3, receipt.UnknownCount);
        Assert.Equal(0, receipt.AttentionCount);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unknown = await database.SecurityFindings.Where(item => item.Status == "unknown").ToListAsync();
        Assert.Equal(3, unknown.Count);
        Assert.All(unknown, finding => Assert.Equal("informational", finding.Severity));
    }

    [Theory]
    [InlineData("unknown", null, 0)]
    [InlineData("notApplicable", null, 0)]
    [InlineData("attention", 0, 100)]
    public async Task UniformStatuses_HaveExplicitScoreAndCoverage(string status, int? expectedScore, int expectedCoverage)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var (_, credential) = await PairAsync(factory, client);
        using var request = Submit(Report(factory, status), credential.DeviceToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var receipt = await response.Content.ReadFromJsonAsync<AnalysisReceipt>();
        Assert.NotNull(receipt);
        Assert.Equal(expectedScore, receipt.Score);
        Assert.Equal(expectedCoverage, receipt.CoveragePercent);
        Assert.Equal(0, receipt.PassedCount);
        using var scope = factory.Services.CreateScope();
        var findings = await scope.ServiceProvider.GetRequiredService<AppDbContext>().SecurityFindings.ToListAsync();
        Assert.All(findings, finding => Assert.Equal(status == "attention"
            ? WindowsRuleCatalog.Rules.Single(rule => rule.Id == finding.RuleId).Severity : "informational", finding.Severity));
    }

    [Fact]
    public async Task NotApplicable_IsExcludedFromPointsAndCoverageDenominators()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var (_, credential) = await PairAsync(factory, client);
        var report = Report(factory);
        report = report with { Checks = report.Checks.Select((check, index) => index == 0 ? check with { Status = "notApplicable" } : check).ToArray() };
        using var request = Submit(report, credential.DeviceToken);
        using var response = await client.SendAsync(request);
        var receipt = await response.Content.ReadFromJsonAsync<AnalysisReceipt>();
        Assert.NotNull(receipt);
        Assert.Equal(100, receipt.Score);
        Assert.Equal(100, receipt.CoveragePercent);
        Assert.Equal(1, receipt.NotApplicableCount);
    }

    [Fact]
    public async Task RetryingSameReport_IsIdempotentRegardlessOfCheckOrderAndChangedReuseConflicts()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var (_, credential) = await PairAsync(factory, client);
        var report = Report(factory);
        using var firstRequest = Submit(report, credential.DeviceToken);
        using var first = await client.SendAsync(firstRequest);
        var original = await first.Content.ReadFromJsonAsync<AnalysisReceipt>();
        using var repeatedRequest = Submit(report with { Checks = report.Checks.Reverse().ToArray() }, credential.DeviceToken);
        using var repeated = await client.SendAsync(repeatedRequest);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        var receipt = await repeated.Content.ReadFromJsonAsync<AnalysisReceipt>();
        Assert.NotNull(receipt);
        Assert.True(receipt.AlreadyReceived);
        Assert.Equal(original!.AnalysisId, receipt.AnalysisId);
        Assert.Equal(original.ReceivedAtUtc, receipt.ReceivedAtUtc);
        using var changedRequest = Submit(report with { Checks = report.Checks.Select((check, index) => index == 0
            ? check with { Evidence = "Otro contenido." } : check).ToArray() }, credential.DeviceToken);
        using var conflict = await client.SendAsync(changedRequest);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await database.SecurityAnalyses.CountAsync());
        Assert.Equal(10, await database.SecurityFindings.CountAsync());
    }

    [Theory]
    [InlineData("unknownRule")]
    [InlineData("duplicateRule")]
    [InlineData("missingRule")]
    [InlineData("ruleVersion")]
    [InlineData("schemaVersion")]
    [InlineData("reportId")]
    [InlineData("status")]
    [InlineData("evidence")]
    [InlineData("control")]
    [InlineData("duration")]
    [InlineData("past")]
    [InlineData("future")]
    [InlineData("offset")]
    [InlineData("observation")]
    [InlineData("agentVersion")]
    [InlineData("nullChecks")]
    [InlineData("nullEntry")]
    [InlineData("userId")]
    [InlineData("score")]
    [InlineData("title")]
    [InlineData("severity")]
    [InlineData("recommendation")]
    public async Task MalformedOrClientOwnedDecisions_AreRejectedWithoutPersistence(string malformed)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var (_, credential) = await PairAsync(factory, client);
        var report = Report(factory);
        var body = JsonSerializer.SerializeToNode(report, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        var checks = body["checks"]!.AsArray();
        var first = checks[0]!.AsObject();
        switch (malformed)
        {
            case "unknownRule": first["ruleId"] = "WIN-INVENTED"; break;
            case "duplicateRule": first["ruleId"] = checks[1]!["ruleId"]!.GetValue<string>(); break;
            case "missingRule": checks.RemoveAt(0); break;
            case "ruleVersion": first["ruleVersion"] = 2; break;
            case "schemaVersion": body["schemaVersion"] = 1; break;
            case "reportId": body["reportId"] = Guid.Empty; break;
            case "status": first["status"] = "healthy"; break;
            case "evidence": first["evidence"] = new string('x', 4001); break;
            case "control": first["evidence"] = "Evidencia\0oculta"; break;
            case "duration": body["startedAtUtc"] = report.CompletedAtUtc.AddMinutes(-31); break;
            case "past": body["startedAtUtc"] = report.StartedAtUtc.AddDays(-8); break;
            case "future": body["completedAtUtc"] = report.CompletedAtUtc.AddMinutes(6); break;
            case "offset": body["completedAtUtc"] = report.CompletedAtUtc.ToOffset(TimeSpan.FromHours(-3)); break;
            case "observation": first["observedAtUtc"] = report.CompletedAtUtc.AddSeconds(6); break;
            case "agentVersion": body["agentVersion"] = " "; break;
            case "nullChecks": body["checks"] = null; break;
            case "nullEntry": checks[0] = null; break;
            case "userId": body["userId"] = Guid.NewGuid(); break;
            case "score": body["score"] = 100; break;
            default: first[malformed] = "Decisión del cliente"; break;
        }
        using var request = Submit(body, credential.DeviceToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().SecurityAnalyses.CountAsync());
    }

    [Fact]
    public async Task Submission_RejectsUserJwtAndBearerDeviceTokenAndMissingAuthentication()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var (owner, credential) = await PairAsync(factory, client);
        foreach (var (token, scheme) in new[] { (owner.AccessToken, "Bearer"), (credential.DeviceToken, "Bearer"), ((string?)null, "Device") })
        {
            using var request = Submit(Report(factory), token, scheme);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("revoke")]
    [InlineData("expiry")]
    [InlineData("reset")]
    public async Task Submission_AfterCredentialInvalidationCannotPersist(string invalidation)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, account);
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var credential = await DeviceApiTestSupport.PairAsync(client, code.PairingCode);
        if (invalidation == "revoke")
        {
            using var revokeRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, $"/api/devices/{code.Device.Id}/revoke", owner.AccessToken);
            using var revoke = await client.SendAsync(revokeRequest);
            Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        }
        else if (invalidation == "expiry") factory.Clock.Advance(TimeSpan.FromDays(91));
        else
        {
            using var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email });
            using var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new
            { token = factory.Email.TokenFor(account.Email, AccountEmailPurpose.PasswordReset), password = AccountCredentials.New().Password });
            Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        }
        using var request = Submit(Report(factory), credential.DeviceToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().SecurityAnalyses.CountAsync());
    }

    [Fact]
    public async Task Submission_IsRateLimitedWithRetryAfter()
    {
        using var factory = new AccountApiFactory(analysisPermitLimit: 2);
        using var client = factory.CreateApiClient();
        var (_, credential) = await PairAsync(factory, client);
        for (var index = 0; index < 2; index++)
        {
            using var request = Submit(Report(factory), credential.DeviceToken);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        using var limitedRequest = Submit(Report(factory), credential.DeviceToken);
        using var limited = await client.SendAsync(limitedRequest);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
    }

    [Fact]
    public async Task OversizedUtf8Report_IsRejectedBeforePersistence()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var (_, credential) = await PairAsync(factory, client);
        var report = Report(factory);
        report = report with { Checks = report.Checks.Select(check => check with { Evidence = new string('界', 4000) }).ToArray() };
        using var request = Submit(report, credential.DeviceToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().SecurityAnalyses.CountAsync());
    }

    [Fact]
    public async Task UnknownOnlyResult_ReachesOwnedHistoryAndDashboardAsNullScore()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var (owner, credential) = await PairAsync(factory, client);
        using var submit = Submit(Report(factory, "unknown"), credential.DeviceToken);
        using var accepted = await client.SendAsync(submit);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        using var detailRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, $"/api/devices/{credential.DeviceId}", owner.AccessToken);
        using var detailResponse = await client.SendAsync(detailRequest);
        var detail = await detailResponse.Content.ReadFromJsonAsync<DeviceDetailResponse>();
        Assert.NotNull(detail);
        Assert.Null(Assert.Single(detail.Analyses).Score);
        Assert.Equal(0, detail.Analyses[0].CoveragePercent);
        Assert.All(detail.Analyses[0].Findings, finding => Assert.Equal("unknown", finding.Status));
        using var dashboardRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/dashboard", owner.AccessToken);
        using var dashboardResponse = await client.SendAsync(dashboardRequest);
        using var dashboard = JsonDocument.Parse(await dashboardResponse.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, dashboard.RootElement.GetProperty("securityScore").ValueKind);
        Assert.Equal(10, dashboard.RootElement.GetProperty("lastAnalysis").GetProperty("unknownCount").GetInt32());
        var other = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        using var otherRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, $"/api/devices/{credential.DeviceId}", other.AccessToken);
        using var hidden = await client.SendAsync(otherRequest);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }
}
