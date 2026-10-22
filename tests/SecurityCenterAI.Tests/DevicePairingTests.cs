using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class DevicePairingTests
{
    [Fact]
    public async Task PairingAndCredential_AreStoredAsHashesAndHaveBoundedLifetimes()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        Assert.Equal(TimeSpan.FromMinutes(factory.DeviceOptions.PairingMinutes),
            code.ExpiresAtUtc - factory.Clock.GetUtcNow().UtcDateTime);
        var credential = await DeviceApiTestSupport.PairAsync(client, code.PairingCode);
        Assert.Equal(code.Device.Id, credential.DeviceId);
        Assert.Equal(TimeSpan.FromDays(factory.DeviceOptions.CredentialDays),
            credential.ExpiresAtUtc - factory.Clock.GetUtcNow().UtcDateTime);

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedCode = await database.DevicePairingCodes.SingleAsync();
        var storedCredential = await database.DeviceCredentials.SingleAsync();
        var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.PairingCode)));
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential.DeviceToken)));
        Assert.True(storedCode.CodeHash == codeHash);
        Assert.True(storedCredential.TokenHash == tokenHash);
        Assert.NotNull(storedCode.ConsumedAtUtc);
    }

    [Fact]
    public async Task PairingCode_IsSingleUseAndCannotBeReplayed()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        await DeviceApiTestSupport.PairAsync(client, code.PairingCode);
        using var replay = await client.PostAsJsonAsync("/api/agent/pair",
            new { pairingCode = code.PairingCode, agentVersion = "0.0.1-test" });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task UnusedPreviousCode_IsRejectedAfterRegeneration()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var first = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Post,
            $"/api/devices/{first.Device.Id}/pairing-code", owner.AccessToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var replacement = await response.Content.ReadFromJsonAsync<TestDevicePairingCode>();
        Assert.NotNull(replacement);
        using var old = await client.PostAsJsonAsync("/api/agent/pair", new { pairingCode = first.PairingCode, agentVersion = "0.0.1-test" });
        Assert.Equal(HttpStatusCode.BadRequest, old.StatusCode);
        await DeviceApiTestSupport.PairAsync(client, replacement.PairingCode);
    }

    [Fact]
    public async Task ExpiredPairingCode_IsRejectedAndCanBeReplacedByItsOwner()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        factory.Clock.Advance(TimeSpan.FromMinutes(factory.DeviceOptions.PairingMinutes).Add(TimeSpan.FromSeconds(1)));
        using var expired = await client.PostAsJsonAsync("/api/agent/pair", new { pairingCode = code.PairingCode, agentVersion = "0.0.1-test" });
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Post,
            $"/api/devices/{code.Device.Id}/pairing-code", owner.AccessToken);
        using var regenerated = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, regenerated.StatusCode);
        var replacement = await regenerated.Content.ReadFromJsonAsync<TestDevicePairingCode>();
        Assert.NotNull(replacement);
        Assert.True(replacement.PairingCode != code.PairingCode);
        await DeviceApiTestSupport.PairAsync(client, replacement.PairingCode);
    }

    [Fact]
    public async Task Regeneration_InvalidatesTheOldCodeAndRepairInvalidatesTheOldCredential()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var first = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var firstCredential = await DeviceApiTestSupport.PairAsync(client, first.PairingCode);
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Post,
            $"/api/devices/{first.Device.Id}/pairing-code", owner.AccessToken);
        using var regenerated = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, regenerated.StatusCode);
        var replacement = await regenerated.Content.ReadFromJsonAsync<TestDevicePairingCode>();
        Assert.NotNull(replacement);
        using var oldCode = await client.PostAsJsonAsync("/api/agent/pair", new { pairingCode = first.PairingCode, agentVersion = "0.0.1-test" });
        Assert.Equal(HttpStatusCode.BadRequest, oldCode.StatusCode);
        var current = await DeviceApiTestSupport.PairAsync(client, replacement.PairingCode);
        using var oldRequest = DeviceApiTestSupport.Heartbeat(firstCredential.DeviceToken);
        using var oldHeartbeat = await client.SendAsync(oldRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, oldHeartbeat.StatusCode);
        using var currentRequest = DeviceApiTestSupport.Heartbeat(current.DeviceToken);
        using var currentHeartbeat = await client.SendAsync(currentRequest);
        Assert.Equal(HttpStatusCode.NoContent, currentHeartbeat.StatusCode);
    }

    [Fact]
    public async Task Heartbeat_UsesServerTimeAndDeviceCredentialsDoNotAuthorizeAccountEndpoints()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var credential = await DeviceApiTestSupport.PairAsync(client, code.PairingCode);
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        using var heartbeatRequest = DeviceApiTestSupport.Heartbeat(credential.DeviceToken);
        using var heartbeat = await client.SendAsync(heartbeatRequest);
        Assert.Equal(HttpStatusCode.NoContent, heartbeat.StatusCode);
        using var detailRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get,
            $"/api/devices/{code.Device.Id}", owner.AccessToken);
        using var detailResponse = await client.SendAsync(detailRequest);
        var detail = await detailResponse.Content.ReadFromJsonAsync<TestDeviceDetail>();
        Assert.NotNull(detail);
        Assert.Equal(factory.Clock.GetUtcNow().UtcDateTime, detail.Device.LastSeenAtUtc);
        Assert.Equal("linked", detail.Device.Status);
        Assert.Equal("0.0.2-test", detail.Device.AgentVersion);
        Assert.Null(detail.Device.Platform);

        foreach (var scheme in new[] { "Bearer", "Device" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(scheme, credential.DeviceToken);
            using var denied = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        using var jwtHeartbeat = DeviceApiTestSupport.Heartbeat(owner.AccessToken, "Bearer");
        using var rejected = await client.SendAsync(jwtHeartbeat);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }

    [Fact]
    public async Task Revocation_RejectsHeartbeatAndAnyOutstandingPairingCode()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, AccountCredentials.New());
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var credential = await DeviceApiTestSupport.PairAsync(client, code.PairingCode);
        using var revokeRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Post,
            $"/api/devices/{code.Device.Id}/revoke", owner.AccessToken);
        using var revoke = await client.SendAsync(revokeRequest);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        using var heartbeatRequest = DeviceApiTestSupport.Heartbeat(credential.DeviceToken);
        using var heartbeat = await client.SendAsync(heartbeatRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, heartbeat.StatusCode);
        using var pair = await client.PostAsJsonAsync("/api/agent/pair", new { pairingCode = code.PairingCode, agentVersion = "0.0.1-test" });
        Assert.Equal(HttpStatusCode.BadRequest, pair.StatusCode);
    }

    [Fact]
    public async Task ExpiredCredential_MakesADevicePendingRatherThanLinkedAndKeepsItsSlot()
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, account);
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var credential = await DeviceApiTestSupport.PairAsync(client, code.PairingCode);
        factory.Clock.Advance(TimeSpan.FromDays(factory.DeviceOptions.CredentialDays + 1));
        using var heartbeatRequest = DeviceApiTestSupport.Heartbeat(credential.DeviceToken);
        using var heartbeat = await client.SendAsync(heartbeatRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, heartbeat.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, account.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AccountAuthentication>();
        Assert.NotNull(auth);
        using var detailRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get,
            $"/api/devices/{code.Device.Id}", auth.AccessToken);
        using var detailResponse = await client.SendAsync(detailRequest);
        var detail = await detailResponse.Content.ReadFromJsonAsync<TestDeviceDetail>();
        Assert.NotNull(detail);
        Assert.Equal("pending", detail.Device.Status);
        Assert.NotNull(detail.Device.PairedAtUtc);
        Assert.Null(detail.Device.CredentialExpiresAtUtc);
        using var duplicateRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, "/api/devices", auth.AccessToken);
        duplicateRequest.Content = JsonContent.Create(new { name = "Otro equipo" });
        using var duplicate = await client.SendAsync(duplicateRequest);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task AgentPairing_IsRateLimitedWithoutUsingAnAccountOrSendingEmail()
    {
        using var factory = new AccountApiFactory(agentPairPermitLimit: 2);
        using var client = factory.CreateApiClient();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var invalid = await client.PostAsJsonAsync("/api/agent/pair", new { pairingCode = OpaqueToken.Create(), agentVersion = "0.0.1-test" });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        using var limited = await client.PostAsJsonAsync("/api/agent/pair", new { pairingCode = OpaqueToken.Create(), agentVersion = "0.0.1-test" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PasswordReset_RevokesPendingCodesAndDeviceCredentialsWithoutFreeingTheSlot(bool paired)
    {
        using var factory = new AccountApiFactory();
        using var client = factory.CreateApiClient();
        var account = AccountCredentials.New();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, account);
        var code = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var credential = paired ? await DeviceApiTestSupport.PairAsync(client, code.PairingCode) : null;
        using var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        var token = factory.Email.TokenFor(account.Email, AccountEmailPurpose.PasswordReset);
        var replacement = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        using var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = replacement });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using var oldCode = await client.PostAsJsonAsync("/api/agent/pair", new { pairingCode = code.PairingCode, agentVersion = "0.0.1-test" });
        Assert.Equal(HttpStatusCode.BadRequest, oldCode.StatusCode);
        if (credential is not null)
        {
            using var heartbeatRequest = DeviceApiTestSupport.Heartbeat(credential.DeviceToken);
            using var heartbeat = await client.SendAsync(heartbeatRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, heartbeat.StatusCode);
        }
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { account.Email, password = replacement });
        var current = await login.Content.ReadFromJsonAsync<AccountAuthentication>();
        Assert.NotNull(current);
        using var detailRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get,
            $"/api/devices/{code.Device.Id}", current.AccessToken);
        using var detailResponse = await client.SendAsync(detailRequest);
        var detail = await detailResponse.Content.ReadFromJsonAsync<TestDeviceDetail>();
        Assert.NotNull(detail);
        Assert.Equal("pending", detail.Device.Status);
        Assert.Null(detail.Device.RevokedAtUtc);
        using var duplicateRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, "/api/devices", current.AccessToken);
        duplicateRequest.Content = JsonContent.Create(new { name = "Otro equipo" });
        using var duplicate = await client.SendAsync(duplicateRequest);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }
}
