using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityCenterAI.Agent.Transport;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Infrastructure.Persistence;
using SecurityCenterAI.Tests;

namespace SecurityCenterAI.Agent.Tests;

/// <summary>Real API pipeline and isolated EF InMemory only; no user database or network socket.</summary>
public sealed class AgentApiIntegrationTests
{
    [Fact]
    public async Task Agent_PairsAndSendsHeartbeatThroughTheRealApiContract()
    {
        using var factory = new AccountApiFactory();
        using var http = factory.CreateApiClient();
        var (authentication, code) = await RegisterDeviceAsync(factory, http);
        var agent = new AgentApiClient(http, http.BaseAddress!, factory.Clock);

        var credential = await agent.PairAsync(code.PairingCode, "0.2.0-contract-test", CancellationToken.None);
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await agent.HeartbeatAsync(credential, "0.2.0-contract-test", CancellationToken.None);

        Assert.Equal(code.Device.Id, credential.DeviceId);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await database.Devices.SingleAsync();
        Assert.Equal(factory.Clock.GetUtcNow().UtcDateTime, device.LastSeenAtUtc);
        Assert.Equal("0.2.0-contract-test", device.AgentVersion);
        Assert.Null(device.Platform);
        Assert.Empty(await database.SecurityAnalyses.ToListAsync());

        using var wrongScheme = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", credential.DeviceToken);
        using var denied = await http.SendAsync(wrongScheme);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.NotNull(authentication.User);
    }

    [Fact]
    public async Task Agent_ObservesServerRevocationWithoutExposingTheCredential()
    {
        using var factory = new AccountApiFactory();
        using var http = factory.CreateApiClient();
        var (authentication, code) = await RegisterDeviceAsync(factory, http);
        var agent = new AgentApiClient(http, http.BaseAddress!, factory.Clock);
        var credential = await agent.PairAsync(code.PairingCode, "0.2.0-contract-test", CancellationToken.None);
        using var revokeRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Post,
            $"/api/devices/{code.Device.Id}/revoke", authentication.AccessToken);
        using var revoke = await http.SendAsync(revokeRequest);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var exception = await Assert.ThrowsAsync<AgentException>(() =>
            agent.HeartbeatAsync(credential, "0.2.0-contract-test", CancellationToken.None));

        Assert.False(exception.ToString().Contains(credential.DeviceToken, StringComparison.Ordinal));
        Assert.False(exception.ToString().Contains(code.PairingCode, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Agent_RequiresANewCodeForRepairAndThePreviousCredentialStopsWorking()
    {
        using var factory = new AccountApiFactory();
        using var http = factory.CreateApiClient();
        var (authentication, code) = await RegisterDeviceAsync(factory, http);
        var agent = new AgentApiClient(http, http.BaseAddress!, factory.Clock);
        var previous = await agent.PairAsync(code.PairingCode, "0.2.0-contract-test", CancellationToken.None);
        await Assert.ThrowsAsync<AgentException>(() =>
            agent.PairAsync(code.PairingCode, "0.2.0-contract-test", CancellationToken.None));
        using var reissueRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Post,
            $"/api/devices/{code.Device.Id}/pairing-code", authentication.AccessToken);
        using var reissue = await http.SendAsync(reissueRequest);
        Assert.Equal(HttpStatusCode.OK, reissue.StatusCode);
        var replacement = await reissue.Content.ReadFromJsonAsync<DevicePairingResponse>();
        Assert.NotNull(replacement);

        var current = await agent.PairAsync(replacement.PairingCode, "0.2.0-contract-test", CancellationToken.None);

        await Assert.ThrowsAsync<AgentException>(() =>
            agent.HeartbeatAsync(previous, "0.2.0-contract-test", CancellationToken.None));
        await agent.HeartbeatAsync(current, "0.2.0-contract-test", CancellationToken.None);
        Assert.Equal(previous.DeviceId, current.DeviceId);
        Assert.True(previous.DeviceToken != current.DeviceToken);
    }

    private static async Task<(AccountAuthentication Authentication, DevicePairingResponse Code)> RegisterDeviceAsync(
        AccountApiFactory factory, HttpClient http)
    {
        var account = AccountCredentials.New();
        var authentication = await factory.RegisterAsync(http, account);
        var verificationToken = factory.Email.TokenFor(account.Email, AccountEmailPurpose.EmailVerification);
        using var verification = await http.PostAsJsonAsync("/api/auth/verify-email", new { token = verificationToken });
        Assert.Equal(HttpStatusCode.NoContent, verification.StatusCode);
        using var createRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, "/api/devices", authentication.AccessToken);
        createRequest.Content = JsonContent.Create(new { name = "Equipo aislado del agente" });
        using var created = await http.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var code = await created.Content.ReadFromJsonAsync<DevicePairingResponse>();
        Assert.NotNull(code);
        return (authentication, code);
    }
}
