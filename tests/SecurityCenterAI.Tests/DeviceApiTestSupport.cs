using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SecurityCenterAI.Api.Services;

namespace SecurityCenterAI.Tests;

internal static class DeviceApiTestSupport
{
    public static async Task<AccountAuthentication> RegisterVerifiedAsync(AccountApiFactory factory,
        HttpClient client, AccountCredentials account)
    {
        var auth = await factory.RegisterAsync(client, account);
        var token = factory.Email.TokenFor(account.Email, AccountEmailPurpose.EmailVerification);
        using var response = await client.PostAsJsonAsync("/api/auth/verify-email", new { token });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return auth;
    }

    public static async Task<TestDevicePairingCode> CreateAsync(HttpClient client, string accessToken,
        string name = "Equipo de prueba")
    {
        using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Post, "/api/devices", accessToken);
        request.Content = JsonContent.Create(new { name });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TestDevicePairingCode>();
        Assert.NotNull(result);
        Assert.True(!string.IsNullOrWhiteSpace(result.PairingCode));
        return result;
    }

    public static async Task<TestDeviceCredential> PairAsync(HttpClient client, string pairingCode)
    {
        using var response = await client.PostAsJsonAsync("/api/agent/pair",
            new { pairingCode, agentVersion = "0.0.1-test" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TestDeviceCredential>();
        Assert.NotNull(result);
        Assert.True(!string.IsNullOrWhiteSpace(result.DeviceToken));
        return result;
    }

    public static HttpRequestMessage Heartbeat(string token, string scheme = "Device") => new(HttpMethod.Post, "/api/agent/heartbeat")
    {
        Headers = { Authorization = new AuthenticationHeaderValue(scheme, token) },
        Content = JsonContent.Create(new { agentVersion = "0.0.2-test" })
    };
}

internal sealed record TestDevice(Guid Id, string Name, string Status,
    DateTime CreatedAtUtc, DateTime? PairedAtUtc, DateTime? LastSeenAtUtc, DateTime? RevokedAtUtc,
    string? Platform, string? AgentVersion, DateTime? CredentialExpiresAtUtc);
internal sealed record TestDevicePairingCode(TestDevice Device, string PairingCode, DateTime ExpiresAtUtc);
internal sealed record TestDeviceCredential(Guid DeviceId, string DeviceToken, DateTime ExpiresAtUtc);
internal sealed record TestDeviceDetail(TestDevice Device, TestDeviceAnalysis[] Analyses);
internal sealed record TestDeviceAnalysis(Guid Id, int Score, DateTime AnalyzedAtUtc, TestDeviceFinding[] Findings);
internal sealed record TestDeviceFinding(Guid Id, string RuleId, int RuleVersion, string Title,
    string Severity, string Evidence, string Recommendation, DateTime DetectedAtUtc);
