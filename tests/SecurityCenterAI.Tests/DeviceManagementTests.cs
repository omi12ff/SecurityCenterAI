using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class DeviceManagementTests
{
    [Fact]
    public async Task UnverifiedAccount_CanReadButCannotCreateUntilVerificationReachesTheDatabase()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var auth = await factory.RegisterAsync(client, account);
        using var listRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/devices", auth.AccessToken);
        using var list = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Empty((await list.Content.ReadFromJsonAsync<TestDevice[]>())!);
        using var deniedRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, "/api/devices", auth.AccessToken);
        deniedRequest.Content = JsonContent.Create(new { name = "Equipo de prueba" });
        using var denied = await client.SendAsync(deniedRequest);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var token = factory.Email.TokenFor(account.Email, SecurityCenterAI.Api.Services.AccountEmailPurpose.EmailVerification);
        using var verification = await client.PostAsJsonAsync("/api/auth/verify-email", new { token });
        Assert.Equal(HttpStatusCode.NoContent, verification.StatusCode);
        // Use the existing JWT: verification must use current account state, not a stale claim.
        var created = await DeviceApiTestSupport.CreateAsync(client, auth.AccessToken);
        Assert.Equal("pending", created.Device.Status);
        Assert.Null(created.Device.CredentialExpiresAtUtc);
    }

    [Fact]
    public async Task ASecondActiveDeviceIsRejected_AndRevocationFreesTheAccountSlot()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var auth = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var first = await DeviceApiTestSupport.CreateAsync(client, auth.AccessToken);
        using var secondRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, "/api/devices", auth.AccessToken);
        secondRequest.Content = JsonContent.Create(new { name = "Segundo equipo" });
        using var rejected = await client.SendAsync(secondRequest);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        using var revokeRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Post,
            $"/api/devices/{first.Device.Id}/revoke", auth.AccessToken);
        using var revoked = await client.SendAsync(revokeRequest);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        var replacement = await DeviceApiTestSupport.CreateAsync(client, auth.AccessToken, "Equipo nuevo");
        Assert.NotEqual(first.Device.Id, replacement.Device.Id);
    }

    [Fact]
    public async Task DeviceManagement_HidesOtherAccountsResourcesAndSecrets()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var own = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var other = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var created = await DeviceApiTestSupport.CreateAsync(client, own.AccessToken);

        foreach (var (method, suffix) in new[]
                 { (HttpMethod.Get, ""), (HttpMethod.Post, "/pairing-code"), (HttpMethod.Post, "/revoke") })
        {
            using var request = AccountApiFactory.AuthorizedRequest(method,
                $"/api/devices/{created.Device.Id}{suffix}", other.AccessToken);
            using var denied = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        using var ownListRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/devices", own.AccessToken);
        using var ownList = await client.SendAsync(ownListRequest);
        var list = await ownList.Content.ReadFromJsonAsync<TestDevice[]>();
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal(created.Device.Id, list[0].Id);
        var raw = await ownList.Content.ReadAsStringAsync();
        Assert.False(raw.Contains(created.PairingCode, StringComparison.Ordinal));
        Assert.False(raw.Contains("codeHash", StringComparison.OrdinalIgnoreCase));
        Assert.False(raw.Contains("tokenHash", StringComparison.OrdinalIgnoreCase));
        using var otherListRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/devices", other.AccessToken);
        using var otherList = await client.SendAsync(otherListRequest);
        Assert.Empty((await otherList.Content.ReadFromJsonAsync<TestDevice[]>())!);
    }

    [Fact]
    public async Task AnUnverifiedOwner_CanReadAndRevokeButCannotRegenerateACode()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var auth = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var created = await DeviceApiTestSupport.CreateAsync(client, auth.AccessToken);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await database.Users.SingleAsync(item => item.Id == auth.User.Id);
            user.EmailVerified = false;
            user.ConcurrencyStamp = Guid.NewGuid();
            await database.SaveChangesAsync();
        }
        using var detailRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get,
            $"/api/devices/{created.Device.Id}", auth.AccessToken);
        using var detail = await client.SendAsync(detailRequest);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var regenerateRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Post,
            $"/api/devices/{created.Device.Id}/pairing-code", auth.AccessToken);
        using var regenerate = await client.SendAsync(regenerateRequest);
        Assert.Equal(HttpStatusCode.Forbidden, regenerate.StatusCode);
        using var revokeRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Post,
            $"/api/devices/{created.Device.Id}/revoke", auth.AccessToken);
        using var revoke = await client.SendAsync(revokeRequest);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
    }

    [Fact]
    public async Task Detail_ShowsOnlyOwnedDeviceAnalysesAndExcludesLegacyUnassignedResults()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var own = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var other = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var device = await DeviceApiTestSupport.CreateAsync(client, own.AccessToken);
        var otherDevice = await DeviceApiTestSupport.CreateAsync(client, other.AccessToken);
        var owned = new SecurityAnalysis { UserId = own.User.Id, DeviceId = device.Device.Id, Score = 72 };
        var rule = new SecurityRule
        {
            Id = "UNIT-DEVICE-001", Version = 1, Title = "Regla de prueba",
            Description = "Regla sembrada solamente por la prueba.", CreatedAtUtc = factory.Clock.GetUtcNow().UtcDateTime
        };
        var finding = new SecurityFinding
        {
            Analysis = owned, SecurityAnalysisId = owned.Id, Rule = rule, RuleId = rule.Id, RuleVersion = rule.Version,
            Title = "Hallazgo del equipo", Severity = "informational", Evidence = "Evidencia de prueba",
            Recommendation = "Recomendacion de prueba", DetectedAtUtc = factory.Clock.GetUtcNow().UtcDateTime
        };
        owned.Findings.Add(finding);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.SecurityRules.Add(rule);
            database.SecurityAnalyses.AddRange(owned,
                new SecurityAnalysis { UserId = own.User.Id, DeviceId = null, Score = 12 },
                new SecurityAnalysis { UserId = other.User.Id, DeviceId = otherDevice.Device.Id, Score = 1 });
            await database.SaveChangesAsync();
        }
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get,
            $"/api/devices/{device.Device.Id}", own.AccessToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<TestDeviceDetail>();
        Assert.NotNull(detail);
        Assert.Single(detail.Analyses);
        Assert.Equal(owned.Id, detail.Analyses[0].Id);
        Assert.Equal(72, detail.Analyses[0].Score);
        Assert.Single(detail.Analyses[0].Findings);
        var returned = detail.Analyses[0].Findings[0];
        Assert.Equal(finding.Id, returned.Id);
        Assert.Equal(rule.Id, returned.RuleId);
        Assert.Equal(rule.Version, returned.RuleVersion);
        Assert.Equal(finding.Evidence, returned.Evidence);
        Assert.Equal(finding.Recommendation, returned.Recommendation);
    }
}
