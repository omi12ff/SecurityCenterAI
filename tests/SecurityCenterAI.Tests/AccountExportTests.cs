using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class AccountExportTests
{
    private static HttpRequestMessage ExportRequest(string accessToken, string suffix = "") =>
        AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/account/export" + suffix, accessToken);

    [Fact]
    public async Task Export_EmptyAccountIncludesRealProfileAndNoInventedHistory()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var owner = await factory.RegisterAsync(client, account);
        using var request = ExportRequest(owner.AccessToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var export = (await response.Content.ReadFromJsonAsync<AccountExportResponse>())!;
        Assert.Equal(1, export.ExportVersion);
        Assert.Equal(factory.Clock.GetUtcNow().UtcDateTime, export.ExportedAtUtc);
        Assert.Equal(DateTimeKind.Utc, export.ExportedAtUtc.Kind);
        Assert.Equal(owner.User.Id, export.Profile.Id);
        Assert.Equal(account.Name, export.Profile.Name);
        Assert.Equal(account.Email, export.Profile.Email);
        Assert.False(export.Profile.EmailVerified);
        Assert.Empty(export.Devices);
        Assert.Empty(export.Analyses);
        Assert.Empty(export.DeletedReports);
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync();
        Assert.Equal(user.CreatedAtUtc, export.Profile.CreatedAtUtc);
    }

    [Fact]
    public async Task Export_IsPrivateAttachmentAndContainsOnlyExplicitProductFields()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var pairing = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var credential = await DeviceApiTestSupport.PairAsync(client, pairing.PairingCode);
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var analysis = Analysis(owner.User.Id, credential.DeviceId, now);
        var marker = new DeletedAnalysisReport { DeviceId = credential.DeviceId, ReportId = Guid.NewGuid(), DeletedAtUtc = now };
        var feedback = new AnalysisFeedback { UserId = owner.User.Id, AnalysisId = analysis.Id,
            Rating = "helpful", Comment = "Mi comentario privado <b>original</b>", ExplanationVersion = 1,
            CreatedAtUtc = now, UpdatedAtUtc = now };
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SecurityAnalyses.Add(analysis);
            db.AnalysisFeedback.Add(feedback);
            db.DeletedAnalysisReports.Add(marker);
            await db.SaveChangesAsync();
        }
        using var request = ExportRequest(owner.AccessToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal($"securitycenterai-account-{owner.User.Id:N}.json", response.Content.Headers.ContentDisposition.FileName!.Trim('"'));
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("\n  \"exportVersion\": 1", raw);
        using var json = JsonDocument.Parse(raw);
        var root = json.RootElement;
        Keys(root, "exportVersion", "exportedAtUtc", "profile", "devices", "analyses", "deletedReports", "securityActivity", "pendingEmailChange", "linkMessageUsage");
        Keys(root.GetProperty("profile"), "id", "name", "email", "createdAtUtc", "emailVerified");
        Keys(root.GetProperty("devices")[0], "id", "name", "createdAtUtc", "pairedAtUtc", "lastSeenAtUtc", "revokedAtUtc",
            "platform", "agentVersion", "status", "credentialExpiresAtUtc");
        Keys(root.GetProperty("analyses")[0], "deviceId", "analysis", "feedback");
        Keys(root.GetProperty("analyses")[0].GetProperty("analysis"), "id", "score", "analyzedAtUtc", "findings", "source",
            "scoringVersion", "coveragePercent", "passedCount", "attentionCount", "unknownCount", "notApplicableCount", "agentVersion",
            "startedAtUtc", "completedAtUtc", "reportId");
        Keys(root.GetProperty("analyses")[0].GetProperty("analysis").GetProperty("findings")[0], "id", "ruleId", "ruleVersion",
            "title", "severity", "evidence", "recommendation", "detectedAtUtc", "status");
        Keys(root.GetProperty("analyses")[0].GetProperty("feedback"), "rating", "comment", "explanationVersion", "updatedAtUtc");
        Keys(root.GetProperty("deletedReports")[0], "deviceId", "reportId", "deletedAtUtc");
        var export = JsonSerializer.Deserialize<AccountExportResponse>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.True(export.Profile.EmailVerified);
        Assert.Equal(credential.ExpiresAtUtc, Assert.Single(export.Devices).CredentialExpiresAtUtc);
        var exportedAnalysis = Assert.Single(export.Analyses);
        Assert.Equal(analysis.Id, exportedAnalysis.Analysis.Id);
        Assert.Equal(75, exportedAnalysis.Analysis.Score);
        Assert.Equal(70, exportedAnalysis.Analysis.CoveragePercent);
        Assert.Equal(analysis.Findings.Single().Evidence, Assert.Single(exportedAnalysis.Analysis.Findings).Evidence);
        Assert.Equal(feedback.Comment, exportedAnalysis.Feedback!.Comment);
        Assert.Equal(marker.ReportId, Assert.Single(export.DeletedReports).ReportId);
        foreach (var secret in new[] { owner.AccessToken, owner.RefreshToken, pairing.PairingCode, credential.DeviceToken, analysis.ReportHash! })
            Assert.DoesNotContain(secret, raw, StringComparison.Ordinal);
        using var verification = factory.Services.CreateScope();
        var database = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedCredential = await database.DeviceCredentials.SingleAsync();
        Assert.DoesNotContain(storedCredential.TokenHash, raw, StringComparison.Ordinal);
        var passwordHash = (await database.Users.SingleAsync()).PasswordHash;
        Assert.DoesNotContain(passwordHash, raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_IncludesAllDevicesAnalysesFindingsAndLegacyNullsBeyondScreenLimits()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var pairing = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var credential = await DeviceApiTestSupport.PairAsync(client, pairing.PairingCode);
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var legacy = Analysis(owner.User.Id, null, now, legacy: true);
        legacy.Findings.Clear();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var revoked = Enumerable.Range(0, 102).Select(index => new Device { UserId = owner.User.Id, Name = $"Anterior {index}",
                CreatedAtUtc = now.AddDays(-index - 2), RevokedAtUtc = now.AddDays(-1) }).ToArray();
            db.Devices.AddRange(revoked);
            db.SecurityAnalyses.AddRange(Enumerable.Range(0, 24).Select(index => Analysis(owner.User.Id,
                index == 0 ? revoked[0].Id : credential.DeviceId, now.AddMinutes(-index - 1))));
            db.SecurityAnalyses.Add(legacy);
            for (var index = 0; index < 105; index++)
                legacy.Findings.Add(new SecurityFinding { RuleId = "WIN-TPM-READY", RuleVersion = 1, Title = $"Histórico {index}",
                    Severity = "informational", Evidence = $"Evidencia {index}", Recommendation = "Revisión manual", DetectedAtUtc = now });
            await db.SaveChangesAsync();
        }
        using var request = ExportRequest(owner.AccessToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var export = (await response.Content.ReadFromJsonAsync<AccountExportResponse>())!;
        Assert.Equal(103, export.Devices.Count);
        Assert.Equal(102, export.Devices.Count(item => item.Status == "revoked"));
        Assert.Equal("linked", export.Devices.Single(item => item.Id == credential.DeviceId).Status);
        Assert.All(export.Devices.Where(item => item.Status == "revoked"), item => Assert.Null(item.CredentialExpiresAtUtc));
        Assert.Equal(25, export.Analyses.Count);
        var exportedLegacy = export.Analyses.Single(item => item.Analysis.Id == legacy.Id);
        Assert.Null(exportedLegacy.DeviceId);
        Assert.Null(exportedLegacy.Analysis.Source);
        Assert.Null(exportedLegacy.Analysis.ReportId);
        Assert.Null(exportedLegacy.Analysis.Score);
        Assert.Null(exportedLegacy.Analysis.ScoringVersion);
        Assert.Null(exportedLegacy.Feedback);
        Assert.Equal(105, exportedLegacy.Analysis.Findings.Count);
        Assert.All(exportedLegacy.Analysis.Findings, finding => Assert.Null(finding.Status));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Export_PendingOrExpiredDeviceDoesNotClaimAnActiveCredential(bool expired)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var pairing = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        if (expired)
        {
            await DeviceApiTestSupport.PairAsync(client, pairing.PairingCode);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.DeviceCredentials.SingleAsync()).ExpiresAtUtc = factory.Clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync();
        }
        using var request = ExportRequest(owner.AccessToken);
        using var response = await client.SendAsync(request);
        var export = (await response.Content.ReadFromJsonAsync<AccountExportResponse>())!;
        var device = Assert.Single(export.Devices);
        Assert.Equal("pending", device.Status);
        Assert.Null(device.CredentialExpiresAtUtc);
    }

    [Fact]
    public async Task Export_UsesSessionOwnerAndHidesForeignAndInconsistentLinks()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var foreign = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var ownDevice = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken, "Mi equipo");
        var foreignDevice = await DeviceApiTestSupport.CreateAsync(client, foreign.AccessToken, "Equipo ajeno");
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var ownAnalysis = Analysis(owner.User.Id, ownDevice.Device.Id, now);
        var foreignAnalysis = Analysis(foreign.User.Id, foreignDevice.Device.Id, now);
        var inconsistentDevice = Analysis(owner.User.Id, foreignDevice.Device.Id, now);
        var inconsistentUser = Analysis(foreign.User.Id, ownDevice.Device.Id, now);
        var ownMarker = new DeletedAnalysisReport { DeviceId = ownDevice.Device.Id, ReportId = Guid.NewGuid(), DeletedAtUtc = now };
        var foreignMarker = new DeletedAnalysisReport { DeviceId = foreignDevice.Device.Id, ReportId = Guid.NewGuid(), DeletedAtUtc = now };
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SecurityAnalyses.AddRange(ownAnalysis, foreignAnalysis, inconsistentDevice, inconsistentUser);
            db.DeletedAnalysisReports.AddRange(ownMarker, foreignMarker);
            db.AnalysisFeedback.Add(new AnalysisFeedback { UserId = foreign.User.Id, AnalysisId = ownAnalysis.Id,
                Rating = "incorrect", Comment = "Comentario ajeno", CreatedAtUtc = now, UpdatedAtUtc = now, ExplanationVersion = 1 });
            await db.SaveChangesAsync();
        }
        using var request = ExportRequest(owner.AccessToken, $"?userId={foreign.User.Id}");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var export = JsonSerializer.Deserialize<AccountExportResponse>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(owner.User.Id, export.Profile.Id);
        Assert.Equal(ownDevice.Device.Id, Assert.Single(export.Devices).Id);
        var analysis = Assert.Single(export.Analyses);
        Assert.Equal(ownAnalysis.Id, analysis.Analysis.Id);
        Assert.Null(analysis.Feedback);
        Assert.Equal(ownMarker.ReportId, Assert.Single(export.DeletedReports).ReportId);
        foreach (var hidden in new[] { foreign.User.Email, foreignDevice.Device.Id.ToString(), foreignAnalysis.Id.ToString(),
                     inconsistentDevice.Id.ToString(), inconsistentUser.Id.ToString(), foreignMarker.ReportId.ToString(), "Comentario ajeno" })
            Assert.DoesNotContain(hidden, raw, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("device")]
    [InlineData("invalid")]
    public async Task Export_OnlyAcceptsAnActiveBearerSession(string scheme)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var pairing = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var credential = await DeviceApiTestSupport.PairAsync(client, pairing.PairingCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/account/export");
        if (scheme != "anonymous") request.Headers.Authorization = new AuthenticationHeaderValue(
            scheme == "device" ? "Device" : "Bearer", scheme == "device" ? credential.DeviceToken : "invalid-token");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Content.Headers.ContentDisposition);
    }

    [Fact]
    public async Task Export_HasItsOwnRateLimitAndReturnsRetryAfter()
    {
        using var factory = new AccountApiFactory(accountExportPermitLimit: 1);
        using var client = factory.CreateApiClient();
        var owner = await factory.RegisterAsync(client, AccountCredentials.New());
        using var firstRequest = ExportRequest(owner.AccessToken);
        using var first = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var secondRequest = ExportRequest(owner.AccessToken);
        using var second = await client.SendAsync(secondRequest);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.NotNull(second.Headers.RetryAfter);
        using var profile = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", owner.AccessToken);
        using var available = await client.SendAsync(profile);
        Assert.Equal(HttpStatusCode.OK, available.StatusCode);
    }

    [Fact]
    public async Task Export_DoesNotModifyStoredAccountDeviceOrReportState()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var pairing = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        await DeviceApiTestSupport.PairAsync(client, pairing.PairingCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.SecurityAnalyses.Add(Analysis(owner.User.Id, pairing.Device.Id, factory.Clock.GetUtcNow().UtcDateTime));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var before = await FingerprintAsync(db);
        using var request = ExportRequest(owner.AccessToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, await FingerprintAsync(db));
    }

    private static SecurityAnalysis Analysis(Guid userId, Guid? deviceId, DateTime now, bool legacy = false) => new()
    {
        UserId = userId, DeviceId = deviceId, Score = legacy ? null : 75, AnalyzedAtUtc = now,
        ReportId = legacy ? null : Guid.NewGuid(), ReportHash = legacy ? null : Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
        Source = legacy ? null : "windows-agent", SchemaVersion = legacy ? null : 2, ScoringVersion = legacy ? null : 1,
        AgentVersion = legacy ? null : "0.4.1-test", CoveragePercent = legacy ? null : 70,
        PassedCount = legacy ? null : 7, AttentionCount = legacy ? null : 0, UnknownCount = legacy ? null : 3,
        NotApplicableCount = legacy ? null : 0, StartedAtUtc = legacy ? null : now.AddSeconds(-10), CompletedAtUtc = legacy ? null : now,
        Findings = [new SecurityFinding { RuleId = "WIN-TPM-READY", RuleVersion = 1, Title = "TPM", Severity = "informational",
            Evidence = "Texto original <script>sin ejecutar</script>", Recommendation = "Revisar manualmente", DetectedAtUtc = now,
            Status = legacy ? null : "unknown" }]
    };

    private static void Keys(JsonElement element, params string[] expected) => Assert.Equal(
        expected.Order(StringComparer.Ordinal).ToArray(), element.EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal).ToArray());

    private static async Task<string> FingerprintAsync(AppDbContext db)
    {
        var state = new
        {
            Users = await db.Users.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Devices = await db.Devices.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Credentials = await db.DeviceCredentials.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Pairing = await db.DevicePairingCodes.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Sessions = await db.UserSessions.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Refresh = await db.RefreshCredentials.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            ActionTokens = await db.AccountActionTokens.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Analyses = await db.SecurityAnalyses.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Findings = await db.SecurityFindings.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Feedback = await db.AnalysisFeedback.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Deleted = await db.DeletedAnalysisReports.AsNoTracking().OrderBy(item => item.DeviceId).ThenBy(item => item.ReportId).ToArrayAsync()
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state)));
    }
}
