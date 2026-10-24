using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Domain.Rules;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class AnalysisHistoryTests
{
    private static SubmitAnalysisRequest Report(AccountApiFactory factory)
    {
        var completed = factory.Clock.GetUtcNow();
        return new(Guid.NewGuid(), 2, "0.4.0-test", completed.AddSeconds(-10), completed,
            WindowsRuleCatalog.Rules.Select(rule => new SubmittedCheck(rule.Id, 1, "passed",
                "Evidencia original <b>sin interpretar</b>.", completed.AddSeconds(-1))).ToArray());
    }

    private static async Task<(AccountAuthentication Owner, TestDeviceCredential Credential, SubmitAnalysisRequest Report, AnalysisReceipt Receipt)>
        SeedAsync(AccountApiFactory factory, HttpClient client)
    {
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var credential = await DeviceApiTestSupport.PairAsync(client, code.PairingCode);
        var report = Report(factory);
        using var response = await UploadAsync(client, credential.DeviceToken, report);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (owner, credential, report, (await response.Content.ReadFromJsonAsync<AnalysisReceipt>())!);
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string token, SubmitAnalysisRequest report) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/agent/analyses")
        { Headers = { Authorization = new AuthenticationHeaderValue("Device", token) }, Content = JsonContent.Create(report) });

    private static string Path(Guid deviceId, Guid analysisId) => $"/api/devices/{deviceId}/analyses/{analysisId}";

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, string path, string token, object? body = null)
    {
        var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Delete, path, token);
        request.Content = JsonContent.Create(body ?? new { confirmed = true });
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Export_IsAnAttachmentWithOnlyAllowedOriginalDataAndPrivateFeedback()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var (owner, credential, report, receipt) = await SeedAsync(factory, client);
        var path = Path(credential.DeviceId, receipt.AnalysisId);
        using var feedback = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, path + "/feedback", owner.AccessToken);
        feedback.Content = JsonContent.Create(new { rating = "helpful", comment = "Comentario privado" });
        using var saved = await client.SendAsync(feedback);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, path + "/export", owner.AccessToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal($"securitycenterai-analysis-{receipt.AnalysisId:N}.json",
            response.Content.Headers.ContentDisposition.FileName!.Trim('"'));
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("\n  \"exportVersion\": 1", raw);
        using var json = JsonDocument.Parse(raw);
        Assert.Equal(new[] { "analysis", "device", "exportVersion", "exportedAtUtc", "feedback" },
            json.RootElement.EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "id", "name" }, json.RootElement.GetProperty("device").EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal).ToArray());
        var exported = JsonSerializer.Deserialize<AnalysisExportResponse>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(factory.Clock.GetUtcNow().UtcDateTime, exported.ExportedAtUtc);
        Assert.Equal(DateTimeKind.Utc, exported.ExportedAtUtc.Kind);
        Assert.Equal(receipt.AnalysisId, exported.Analysis.Id);
        Assert.Equal(report.ReportId, exported.Analysis.ReportId);
        Assert.Equal(100, exported.Analysis.Score);
        Assert.Equal(10, exported.Analysis.Findings.Count);
        Assert.All(exported.Analysis.Findings, finding => Assert.Equal(report.Checks[0].Evidence, finding.Evidence));
        Assert.Equal("Comentario privado", exported.Feedback!.Comment);
        Assert.Equal(1, exported.Feedback.ExplanationVersion);
        foreach (var secret in new[] { credential.DeviceToken, owner.AccessToken, owner.RefreshToken, owner.User.Email,
                     "reportHash", "passwordHash", "tokenHash", "credential", "session", "vault" })
            Assert.DoesNotContain(secret, raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Export_PreservesLegacyNullsAndAllFindingsBeyondHistoryDisplayLimit()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var analysis = new SecurityAnalysis { UserId = owner.User.Id, DeviceId = code.Device.Id,
            Score = null, AnalyzedAtUtc = factory.Clock.GetUtcNow().UtcDateTime };
        for (var index = 0; index < 105; index++)
            analysis.Findings.Add(new SecurityFinding { RuleId = "WIN-TPM-READY", RuleVersion = 1,
                Title = $"Histórico {index}", Severity = "informational", Evidence = $"Evidencia {index}", Recommendation = "Revisar" });
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.SecurityAnalyses.Add(analysis);
            await database.SaveChangesAsync();
        }
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get,
            Path(code.Device.Id, analysis.Id) + "/export", owner.AccessToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var exported = await response.Content.ReadFromJsonAsync<AnalysisExportResponse>();
        Assert.NotNull(exported);
        Assert.Equal(105, exported.Analysis.Findings.Count);
        Assert.Null(exported.Analysis.Score);
        Assert.Null(exported.Analysis.Source);
        Assert.Null(exported.Analysis.ScoringVersion);
        Assert.Null(exported.Analysis.ReportId);
        Assert.Null(exported.Feedback);
        Assert.All(exported.Analysis.Findings, finding => Assert.Null(finding.Status));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("type")]
    [InlineData("null")]
    public async Task Delete_RequiresExplicitConfirmationAndRejectsUnexpectedFieldsWithoutChanges(string malformed)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var (owner, credential, _, receipt) = await SeedAsync(factory, client);
        var body = malformed switch
        { "false" => "{\"confirmed\":false}", "missing" => "{}", "extra" => "{\"confirmed\":true,\"all\":true}",
          "type" => "{\"confirmed\":\"true\"}", _ => "null" };
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Delete, Path(credential.DeviceId, receipt.AnalysisId), owner.AccessToken);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.SecurityAnalyses.CountAsync());
        Assert.Equal(10, await db.SecurityFindings.CountAsync());
        Assert.Equal(0, await db.DeletedAnalysisReports.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_BoundsDeclaredAndChunkedBodies(bool chunked)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var (owner, credential, _, receipt) = await SeedAsync(factory, client);
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Delete, Path(credential.DeviceId, receipt.AnalysisId), owner.AccessToken);
        var bytes = Encoding.UTF8.GetBytes("{\"confirmed\":true}" + new string(' ', 1024));
        request.Content = chunked ? new ChunkedContent(bytes) : new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new("application/json");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.SecurityAnalyses.CountAsync());
        Assert.Equal(0, await db.DeletedAnalysisReports.CountAsync());
    }

    [Theory]
    [InlineData("anonymous", false)]
    [InlineData("anonymous", true)]
    [InlineData("device", false)]
    [InlineData("device", true)]
    [InlineData("foreign", false)]
    [InlineData("foreign", true)]
    [InlineData("mismatch", false)]
    [InlineData("mismatch", true)]
    public async Task History_RequiresBearerOwnershipOfBothDeviceAndAnalysis(string actor, bool delete)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var (owner, credential, _, receipt) = await SeedAsync(factory, client);
        var token = actor == "foreign" ? (await factory.RegisterAsync(client, AccountCredentials.New())).AccessToken : owner.AccessToken;
        var path = Path(actor == "mismatch" ? Guid.NewGuid() : credential.DeviceId, receipt.AnalysisId);
        using var request = new HttpRequestMessage(delete ? HttpMethod.Delete : HttpMethod.Get, path + (delete ? "" : "/export"));
        if (actor != "anonymous") request.Headers.Authorization = new(actor == "device" ? "Device" : "Bearer", actor == "device" ? credential.DeviceToken : token);
        if (delete) request.Content = JsonContent.Create(new { confirmed = true });
        using var response = await client.SendAsync(request);
        Assert.Equal(actor is "anonymous" or "device" ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().SecurityAnalyses.CountAsync());
    }

    [Fact]
    public async Task Delete_RemovesOnlySelectedReportAndFeedbackAndPreventsReuploadWhileNewReportsStillWork()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var (owner, credential, report, receipt) = await SeedAsync(factory, client);
        var otherReport = Report(factory);
        using var otherUpload = await UploadAsync(client, credential.DeviceToken, otherReport);
        var other = (await otherUpload.Content.ReadFromJsonAsync<AnalysisReceipt>())!;
        int ruleCount;
        using (var baseline = factory.Services.CreateScope())
            ruleCount = await baseline.ServiceProvider.GetRequiredService<AppDbContext>().SecurityRules.CountAsync();
        var path = Path(credential.DeviceId, receipt.AnalysisId);
        using var feedback = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, path + "/feedback", owner.AccessToken);
        feedback.Content = JsonContent.Create(new { rating = "unclear", comment = "Borrar también este texto" });
        using var saved = await client.SendAsync(feedback);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var deleted = await DeleteAsync(client, path, owner.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(other.AnalysisId, (await db.SecurityAnalyses.SingleAsync()).Id);
            Assert.Equal(10, await db.SecurityFindings.CountAsync());
            Assert.Equal(0, await db.AnalysisFeedback.CountAsync());
            var marker = await db.DeletedAnalysisReports.SingleAsync();
            Assert.Equal(report.ReportId, marker.ReportId);
            Assert.Equal(credential.DeviceId, marker.DeviceId);
            Assert.Equal(factory.Clock.GetUtcNow().UtcDateTime, marker.DeletedAtUtc);
            Assert.Equal(1, await db.DeviceCredentials.CountAsync(item => item.RevokedAtUtc == null));
            Assert.Null((await db.Devices.SingleAsync()).RevokedAtUtc);
            Assert.Equal(ruleCount, await db.SecurityRules.CountAsync());
        }
        using var again = await DeleteAsync(client, path, owner.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        foreach (var endpoint in new[] { path + "/export", path + "/explanation",
                     $"/api/devices/{credential.DeviceId}/analyses/compare?baselineId={receipt.AnalysisId}&targetId={other.AnalysisId}" })
        {
            using var get = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, endpoint, owner.AccessToken);
            using var response = await client.SendAsync(get);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using var detailRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, $"/api/devices/{credential.DeviceId}", owner.AccessToken);
        using var detailResponse = await client.SendAsync(detailRequest);
        var detail = (await detailResponse.Content.ReadFromJsonAsync<DeviceDetailResponse>())!;
        Assert.Equal(other.AnalysisId, Assert.Single(detail.Analyses).Id);
        using var dashboard = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/dashboard", owner.AccessToken);
        using var dashboardResponse = await client.SendAsync(dashboard);
        using var dashboardJson = JsonDocument.Parse(await dashboardResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, dashboardJson.RootElement.GetProperty("totalAnalyses").GetInt32());
        foreach (var retried in new[] { report, report with { AgentVersion = "changed-version" } })
        {
            using var response = await UploadAsync(client, credential.DeviceToken, retried);
            Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
            Assert.Contains("El informe fue eliminado", await response.Content.ReadAsStringAsync());
        }
        using var fresh = await UploadAsync(client, credential.DeviceToken, report with { ReportId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, fresh.StatusCode);
    }

    [Fact]
    public async Task RevokedDevice_CanExportAndDeleteItsHistoryWithoutRevivingAccess()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var (owner, credential, _, receipt) = await SeedAsync(factory, client);
        using var revoke = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, $"/api/devices/{credential.DeviceId}/revoke", owner.AccessToken);
        using var revoked = await client.SendAsync(revoke);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        var path = Path(credential.DeviceId, receipt.AnalysisId);
        using var get = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, path + "/export", owner.AccessToken);
        using var exported = await client.SendAsync(get);
        Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        using var deleted = await DeleteAsync(client, path, owner.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.NotNull((await db.Devices.SingleAsync()).RevokedAtUtc);
        Assert.Equal(0, await db.SecurityAnalyses.CountAsync());
    }

    [Fact]
    public async Task LegacyDelete_DoesNotInventAnAgentReportMarker()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var legacy = new SecurityAnalysis { UserId = owner.User.Id, DeviceId = code.Device.Id, Score = null,
            AnalyzedAtUtc = factory.Clock.GetUtcNow().UtcDateTime };
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SecurityAnalyses.Add(legacy);
            await db.SaveChangesAsync();
        }
        using var deleted = await DeleteAsync(client, Path(code.Device.Id, legacy.Id), owner.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var verification = factory.Services.CreateScope();
        var database = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await database.DeletedAnalysisReports.CountAsync());
        Assert.Equal(0, await database.SecurityAnalyses.CountAsync());
    }

    private sealed class ChunkedContent(byte[] payload) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(payload).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
