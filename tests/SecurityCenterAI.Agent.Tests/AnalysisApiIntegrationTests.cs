using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Agent.Checks;
using SecurityCenterAI.Agent.Transport;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Infrastructure.Persistence;
using SecurityCenterAI.Tests;

namespace SecurityCenterAI.Agent.Tests;

/// <summary>Agent HTTP transport through the real authentication, validation and ingestion pipeline; isolated EF InMemory.</summary>
public sealed class AnalysisApiIntegrationTests
{
    [Fact]
    public async Task Submit_StoresServerOwnedResultsAndRetriesWithoutCreatingAnotherAnalysis()
    {
        using var factory = new AccountApiFactory();
        using var http = factory.CreateApiClient();
        var (owner, pairing) = await RegisterDeviceAsync(factory, http);
        var agent = new AgentApiClient(http, http.BaseAddress!, factory.Clock);
        var credential = await agent.PairAsync(pairing.PairingCode, "0.2.0-contract-test", CancellationToken.None);
        var report = AnalysisReportSamples.Create(factory.Clock.GetUtcNow());

        var created = await agent.SubmitAnalysisAsync(credential, report, CancellationToken.None);
        var repeated = await agent.SubmitAnalysisAsync(credential, report, CancellationToken.None);

        Assert.False(created.AlreadyReceived);
        Assert.True(repeated.AlreadyReceived);
        Assert.Equal(created.AnalysisId, repeated.AnalysisId);
        Assert.Equal(report.ReportId, repeated.ReportId);
        Assert.Equal(100, created.Score);
        Assert.Equal(100, created.CoveragePercent);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var analysis = await database.SecurityAnalyses.Include(item => item.Findings).SingleAsync();
        Assert.Equal(owner.User.Id, analysis.UserId);
        Assert.Equal(credential.DeviceId, analysis.DeviceId);
        Assert.Equal(report.ReportId, analysis.ReportId);
        Assert.Equal("windows-agent", analysis.Source);
        Assert.Equal(2, analysis.SchemaVersion);
        Assert.Equal(1, analysis.ScoringVersion);
        Assert.Equal(10, analysis.Findings.Count);
        Assert.All(analysis.Findings, finding =>
        {
            Assert.Equal("passed", finding.Status);
            Assert.DoesNotContain("Local title", finding.Title);
            Assert.DoesNotContain("Local advice", finding.Recommendation);
            Assert.Equal("Evidencia sintética de una prueba aislada.", finding.Evidence);
        });

        using var detailRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get,
            $"/api/devices/{credential.DeviceId}", owner.AccessToken);
        using var detailResponse = await http.SendAsync(detailRequest);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<DeviceDetailResponse>();
        Assert.NotNull(detail);
        var projected = Assert.Single(detail.Analyses);
        Assert.Equal(created.AnalysisId, projected.Id);
        Assert.Equal(report.ReportId, projected.ReportId);
        Assert.Equal(10, projected.Findings.Count);
        var body = await detailResponse.Content.ReadAsStringAsync();
        Assert.False(body.Contains(credential.DeviceToken, StringComparison.Ordinal));
        Assert.False(body.Contains(pairing.PairingCode, StringComparison.Ordinal));
        Assert.False(body.Contains("reportHash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Submit_DoesNotPresentAnUnknownReportAsAHealthyScore()
    {
        using var factory = new AccountApiFactory();
        using var http = factory.CreateApiClient();
        var (_, pairing) = await RegisterDeviceAsync(factory, http);
        var agent = new AgentApiClient(http, http.BaseAddress!, factory.Clock);
        var credential = await agent.PairAsync(pairing.PairingCode, "0.2.0-contract-test", CancellationToken.None);
        var report = AnalysisReportSamples.Create(factory.Clock.GetUtcNow());
        report = report with { Checks = report.Checks.Select(check => check with { Status = CheckStatus.Unknown }).ToArray() };

        var result = await agent.SubmitAnalysisAsync(credential, report, CancellationToken.None);

        Assert.Null(result.Score);
        Assert.Equal(0, result.CoveragePercent);
        Assert.Equal(10, result.UnknownCount);
        Assert.Equal(0, result.PassedCount);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null((await database.SecurityAnalyses.SingleAsync()).Score);
    }

    [Fact]
    public async Task Submit_RejectsAChangedReportUnderTheSameIdentifierWithoutReplacingStoredEvidence()
    {
        using var factory = new AccountApiFactory();
        using var http = factory.CreateApiClient();
        var (_, pairing) = await RegisterDeviceAsync(factory, http);
        var agent = new AgentApiClient(http, http.BaseAddress!, factory.Clock);
        var credential = await agent.PairAsync(pairing.PairingCode, "0.2.0-contract-test", CancellationToken.None);
        var report = AnalysisReportSamples.Create(factory.Clock.GetUtcNow());
        var first = await agent.SubmitAnalysisAsync(credential, report, CancellationToken.None);
        var changed = report with
        {
            Checks = report.Checks.Select((check, index) => index == 0
                ? check with { Status = CheckStatus.Attention, Evidence = "Otra evidencia sintética." }
                : check).ToArray()
        };

        var exception = await Assert.ThrowsAsync<AgentException>(() =>
            agent.SubmitAnalysisAsync(credential, changed, CancellationToken.None));

        Assert.False(exception.ToString().Contains(credential.DeviceToken, StringComparison.Ordinal));
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await database.SecurityAnalyses.Include(analysis => analysis.Findings).SingleAsync();
        Assert.Equal(first.AnalysisId, stored.Id);
        Assert.Equal(100, stored.Score);
        Assert.DoesNotContain(stored.Findings, finding => finding.Evidence == "Otra evidencia sintética.");
    }

    [Fact]
    public async Task Submit_RejectsARevokedAgentAndDoesNotCreateAResultForAnotherUser()
    {
        using var factory = new AccountApiFactory();
        using var http = factory.CreateApiClient();
        var (owner, pairing) = await RegisterDeviceAsync(factory, http);
        var agent = new AgentApiClient(http, http.BaseAddress!, factory.Clock);
        var credential = await agent.PairAsync(pairing.PairingCode, "0.2.0-contract-test", CancellationToken.None);
        var other = await factory.RegisterAsync(http, AccountCredentials.New());
        using var otherRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get,
            $"/api/devices/{credential.DeviceId}", other.AccessToken);
        using var otherResponse = await http.SendAsync(otherRequest);
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
        using var revokeRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Post,
            $"/api/devices/{credential.DeviceId}/revoke", owner.AccessToken);
        using var revoke = await http.SendAsync(revokeRequest);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var exception = await Assert.ThrowsAsync<AgentException>(() =>
            agent.SubmitAnalysisAsync(credential, AnalysisReportSamples.Create(factory.Clock.GetUtcNow()), CancellationToken.None));

        Assert.False(exception.ToString().Contains(credential.DeviceToken, StringComparison.Ordinal));
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await database.SecurityAnalyses.ToListAsync());
    }

    private static async Task<(AccountAuthentication Owner, DevicePairingResponse Pairing)> RegisterDeviceAsync(
        AccountApiFactory factory, HttpClient http)
    {
        var account = AccountCredentials.New();
        var owner = await factory.RegisterAsync(http, account);
        var token = factory.Email.TokenFor(account.Email, AccountEmailPurpose.EmailVerification);
        using var verification = await http.PostAsJsonAsync("/api/auth/verify-email", new { token });
        Assert.Equal(HttpStatusCode.NoContent, verification.StatusCode);
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, "/api/devices", owner.AccessToken);
        request.Content = JsonContent.Create(new { name = "Equipo aislado de ingesta" });
        using var created = await http.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var pairing = await created.Content.ReadFromJsonAsync<DevicePairingResponse>();
        Assert.NotNull(pairing);
        return (owner, pairing);
    }
}
