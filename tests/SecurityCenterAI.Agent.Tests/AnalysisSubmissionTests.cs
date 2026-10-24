using System.Net;
using System.Text;
using System.Text.Json;
using SecurityCenterAI.Agent.Checks;
using SecurityCenterAI.Agent.Security;
using SecurityCenterAI.Agent.Transport;

namespace SecurityCenterAI.Agent.Tests;

public sealed class AnalysisSubmissionTests
{
    private static readonly Uri ApiUri = new("https://api.example.invalid/");
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 15, 0, 0, TimeSpan.Zero);
    private static readonly string Token = new('A', 43);

    [Theory]
    [InlineData(201, false)]
    [InlineData(200, true)]
    public async Task Submit_SendsOnlyTheEvidenceContractAndAcceptsCreationOrIdempotentReceipt(int status, bool alreadyReceived)
    {
        var report = AnalysisReportSamples.Create(Now);
        var analysisId = Guid.NewGuid();
        var requests = 0;
        using var handler = new CallbackHandler(async (request, cancellationToken) =>
        {
            requests++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new Uri(ApiUri, "api/agent/analyses"), request.RequestUri);
            Assert.Equal("Device", request.Headers.Authorization?.Scheme);
            Assert.True(request.Headers.Authorization?.Parameter == Token);
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.False(body.Contains(Token, StringComparison.Ordinal));
            Assert.False(body.Contains("Local title", StringComparison.Ordinal));
            Assert.False(body.Contains("Local advice", StringComparison.Ordinal));
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            Assert.Equal(new[] { "agentVersion", "checks", "completedAtUtc", "reportId", "schemaVersion", "startedAtUtc" },
                root.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray());
            Assert.Equal(report.ReportId, root.GetProperty("reportId").GetGuid());
            Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(10, root.GetProperty("checks").GetArrayLength());
            foreach (var check in root.GetProperty("checks").EnumerateArray())
            {
                Assert.Equal(new[] { "evidence", "observedAtUtc", "ruleId", "ruleVersion", "status" },
                    check.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray());
                Assert.Equal("passed", check.GetProperty("status").GetString());
            }
            return Receipt(report.ReportId, analysisId, (HttpStatusCode)status, alreadyReceived);
        });
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));

        var result = await api.SubmitAnalysisAsync(Credential(), report, CancellationToken.None);

        Assert.Equal(1, requests);
        Assert.Equal(analysisId, result.AnalysisId);
        Assert.Equal(report.ReportId, result.ReportId);
        Assert.Equal(100, result.Score);
        Assert.Equal(100, result.CoveragePercent);
        Assert.Equal(10, result.PassedCount);
        Assert.Equal(alreadyReceived, result.AlreadyReceived);
    }

    [Theory]
    [InlineData(302)]
    [InlineData(307)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(409)]
    [InlineData(410)]
    [InlineData(413)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task Submit_RedactsFailuresAndNeverRetriesOrFollowsRedirects(int status)
    {
        var requests = 0;
        using var handler = new CallbackHandler((_, _) =>
        {
            requests++;
            var response = new HttpResponseMessage((HttpStatusCode)status)
            {
                Content = new StringContent($"private-response-{Token}")
            };
            response.Headers.Location = new Uri($"https://external.example.invalid/?credential={Token}");
            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));

        var exception = await Assert.ThrowsAsync<AgentException>(() =>
            api.SubmitAnalysisAsync(Credential(), AnalysisReportSamples.Create(Now), CancellationToken.None));

        Assert.Equal(1, requests);
        Assert.False(exception.ToString().Contains(Token, StringComparison.Ordinal));
        Assert.False(exception.ToString().Contains("private-response", StringComparison.Ordinal));
        Assert.False(exception.ToString().Contains("external.example.invalid", StringComparison.Ordinal));
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task DeletedReport_AsksForANewLocalScanWithoutRetryingOrChangingTheReport()
    {
        var report = AnalysisReportSamples.Create(Now);
        var reportId = report.ReportId;
        var requests = 0;
        using var handler = new CallbackHandler((_, _) =>
        {
            requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Gone)
            { Content = new StringContent($"private-response-{Token}") });
        });
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));
        var error = await Assert.ThrowsAsync<AgentException>(() => api.SubmitAnalysisAsync(Credential(), report, CancellationToken.None));
        Assert.Contains("fue eliminado", error.Message);
        Assert.Contains("Ejecutá scan", error.Message);
        Assert.DoesNotContain(Token, error.Message);
        Assert.Equal(1, requests);
        Assert.Equal(reportId, report.ReportId);
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("empty-report-id")]
    [InlineData("nine-checks")]
    [InlineData("duplicate-rule")]
    [InlineData("unknown-rule")]
    [InlineData("wrong-rule-version")]
    [InlineData("invalid-status")]
    [InlineData("future")]
    [InlineData("stale")]
    [InlineData("inverted-time")]
    [InlineData("too-long")]
    [InlineData("observed-outside")]
    public async Task Submit_RejectsUnsafeOrLegacyReportsBeforeSending(string scenario)
    {
        var report = AnalysisReportSamples.Create(Now);
        report = scenario switch
        {
            "legacy" => report with { SchemaVersion = 1 },
            "empty-report-id" => report with { ReportId = Guid.Empty },
            "nine-checks" => report with { Checks = report.Checks.Skip(1).ToArray() },
            "duplicate-rule" => report with { Checks = report.Checks.Select((check, index) => index == 0 ? report.Checks[1] : check).ToArray() },
            "unknown-rule" => ReplaceFirst(report, report.Checks[0] with { RuleId = "UNKNOWN-RULE" }),
            "wrong-rule-version" => ReplaceFirst(report, report.Checks[0] with { RuleVersion = 2 }),
            "invalid-status" => ReplaceFirst(report, report.Checks[0] with { Status = (CheckStatus)999 }),
            "future" => Shift(report, TimeSpan.FromMinutes(6)),
            "stale" => Shift(report, TimeSpan.FromDays(-8)),
            "inverted-time" => report with { StartedAtUtc = Now.AddSeconds(1) },
            "too-long" => report with { StartedAtUtc = Now.AddMinutes(-31) },
            "observed-outside" => ReplaceFirst(report, report.Checks[0] with { ObservedAtUtc = Now.AddMinutes(1) }),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        using var handler = new CallbackHandler((_, _) => throw new InvalidOperationException("An invalid report must not leave the process."));
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));

        await Assert.ThrowsAsync<AgentException>(() => api.SubmitAnalysisAsync(Credential(), report, CancellationToken.None));
    }

    [Fact]
    public async Task Submit_DoesNotSendAnExpiredOrDifferentOriginCredential()
    {
        using var handler = new CallbackHandler((_, _) => throw new InvalidOperationException("Unexpected request."));
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));
        var report = AnalysisReportSamples.Create(Now);

        await Assert.ThrowsAsync<AgentException>(() => api.SubmitAnalysisAsync(
            Credential(expiresAtUtc: Now), report, CancellationToken.None));
        await Assert.ThrowsAsync<AgentException>(() => api.SubmitAnalysisAsync(
            Credential(apiUri: new Uri("https://different.example.invalid/")), report, CancellationToken.None));
    }

    [Fact]
    public async Task Submit_EnforcesTheEncodedPayloadLimitEvenWhenEachEvidenceFieldFitsItsCharacterLimit()
    {
        var report = AnalysisReportSamples.Create(Now);
        report = report with { Checks = report.Checks.Select(check => check with { Evidence = new string('é', 4000) }).ToArray() };
        using var handler = new CallbackHandler((_, _) => throw new InvalidOperationException("An oversized report must not leave the process."));
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));

        await Assert.ThrowsAsync<AgentException>(() => api.SubmitAnalysisAsync(Credential(), report, CancellationToken.None));
    }

    [Fact]
    public async Task Submit_PropagatesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new CallbackHandler(async (_, cancellationToken) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Cancellation must abort the request.");
        });
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            api.SubmitAnalysisAsync(Credential(), AnalysisReportSamples.Create(Now), cancellation.Token));
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("wrong-report")]
    [InlineData("empty-analysis")]
    [InlineData("oversize")]
    public async Task Submit_RejectsInvalidReceiptsWithoutExposingTheirBody(string scenario)
    {
        var report = AnalysisReportSamples.Create(Now);
        using var handler = new CallbackHandler((_, _) => Task.FromResult(scenario switch
        {
            "malformed" => new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent($"{{{Token}") },
            "wrong-report" => Receipt(Guid.NewGuid(), Guid.NewGuid(), HttpStatusCode.Created, false),
            "empty-analysis" => Receipt(report.ReportId, Guid.Empty, HttpStatusCode.Created, false),
            "oversize" => new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(new string('x', 20 * 1024) + Token) },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        }));
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));

        var exception = await Assert.ThrowsAsync<AgentException>(() => api.SubmitAnalysisAsync(Credential(), report, CancellationToken.None));

        Assert.False(exception.ToString().Contains(Token, StringComparison.Ordinal));
        Assert.Null(exception.InnerException);
    }

    private static AgentReport ReplaceFirst(AgentReport report, CheckResult replacement) =>
        report with { Checks = report.Checks.Select((check, index) => index == 0 ? replacement : check).ToArray() };

    private static AgentReport Shift(AgentReport report, TimeSpan interval) => report with
    {
        StartedAtUtc = report.StartedAtUtc.Add(interval), CompletedAtUtc = report.CompletedAtUtc.Add(interval),
        Checks = report.Checks.Select(check => check with { ObservedAtUtc = check.ObservedAtUtc.Add(interval) }).ToArray()
    };

    private static DeviceCredential Credential(Uri? apiUri = null, DateTimeOffset? expiresAtUtc = null) =>
        new(apiUri ?? ApiUri, Guid.NewGuid(), Token, expiresAtUtc ?? Now.AddDays(90));

    private static HttpResponseMessage Receipt(Guid reportId, Guid analysisId, HttpStatusCode status, bool alreadyReceived) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            analysisId, reportId, score = 100, coveragePercent = 100, passedCount = 10, attentionCount = 0,
            unknownCount = 0, notApplicableCount = 0, scoringVersion = 1, receivedAtUtc = Now, alreadyReceived
        }), Encoding.UTF8, "application/json")
    };

    private sealed class CallbackHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            callback(request, cancellationToken);
    }
}
