using System.Net;
using System.Text;
using System.Text.Json;
using SecurityCenterAI.Agent;
using SecurityCenterAI.Agent.Security;
using SecurityCenterAI.Agent.Transport;

namespace SecurityCenterAI.Agent.Tests;

public sealed class AgentApiClientTests
{
    private static readonly Uri ApiUri = new("https://api.example.invalid/");
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid DeviceId = Guid.NewGuid();
    private static readonly string Code = new('B', 43);
    private static readonly string Token = new('A', 43);

    [Fact]
    public async Task Pair_UsesOnlyThePairingCodeAndReturnsTheDeviceCredential()
    {
        var requests = 0;
        using var handler = new CallbackHandler(async (request, cancellationToken) =>
        {
            requests++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new Uri(ApiUri, "api/agent/pair"), request.RequestUri);
            Assert.Null(request.Headers.Authorization);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.True(json.RootElement.GetProperty("pairingCode").GetString() == Code);
            Assert.Equal("0.1.0-test", json.RootElement.GetProperty("agentVersion").GetString());
            Assert.Equal(2, json.RootElement.EnumerateObject().Count());
            return PairResponse();
        });
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));

        var credential = await api.PairAsync(Code, "0.1.0-test", CancellationToken.None);

        Assert.Equal(1, requests);
        Assert.Equal(DeviceId, credential.DeviceId);
        Assert.Equal(ApiUri, credential.ApiBaseUri);
        Assert.Equal(Now.AddDays(90), credential.ExpiresAtUtc);
        Assert.True(credential.DeviceToken == Token);
        Assert.False(credential.ToString()!.Contains(Token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Heartbeat_UsesTheDeviceSchemeAndDoesNotIncludeTheTokenInJson()
    {
        using var handler = new CallbackHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(new Uri(ApiUri, "api/agent/heartbeat"), request.RequestUri);
            Assert.Equal("Device", request.Headers.Authorization?.Scheme);
            Assert.True(request.Headers.Authorization?.Parameter == Token);
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.False(body.Contains(Token, StringComparison.Ordinal));
            using var json = JsonDocument.Parse(body);
            Assert.Equal("0.1.0-test", json.RootElement.GetProperty("agentVersion").GetString());
            Assert.Single(json.RootElement.EnumerateObject());
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));

        await api.HeartbeatAsync(Credential(), "0.1.0-test", CancellationToken.None);
    }

    [Theory]
    [InlineData(302)]
    [InlineData(307)]
    [InlineData(400)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task Pair_RejectsRedirectsAndFailuresWithoutExposingResponseBodies(int status)
    {
        var requests = 0;
        using var handler = new CallbackHandler((_, _) =>
        {
            requests++;
            var response = new HttpResponseMessage((HttpStatusCode)status)
            {
                Content = new StringContent($"private-response-{Code}-{Token}")
            };
            response.Headers.Location = new Uri($"https://external.example.invalid/?credential={Token}");
            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));

        var exception = await Assert.ThrowsAsync<AgentException>(() =>
            api.PairAsync(Code, "0.1.0-test", CancellationToken.None));

        Assert.Equal(1, requests);
        AssertRedacted(exception);
        Assert.False(exception.ToString().Contains("private-response", StringComparison.Ordinal));
        Assert.False(exception.ToString().Contains("external.example.invalid", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public async Task Pair_RejectsMalformedCodesBeforeMakingAnyRequest(string invalidCode)
    {
        using var handler = new CallbackHandler((_, _) => throw new InvalidOperationException("Unexpected request."));
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));

        await Assert.ThrowsAsync<AgentException>(() => api.PairAsync(invalidCode, "0.1.0-test", CancellationToken.None));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("too-long")]
    [InlineData("empty-device")]
    [InlineData("bad-token")]
    public async Task Pair_RejectsInvalidCredentialMetadata(string scenario)
    {
        var expiry = scenario switch
        {
            "expired" => Now,
            "too-long" => Now.AddDays(92),
            _ => Now.AddDays(90)
        };
        using var handler = new CallbackHandler((_, _) => Task.FromResult(PairResponse(
            scenario == "empty-device" ? Guid.Empty : DeviceId,
            scenario == "bad-token" ? "invalid" : Token,
            expiry)));
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));

        var exception = await Assert.ThrowsAsync<AgentException>(() =>
            api.PairAsync(Code, "0.1.0-test", CancellationToken.None));

        AssertRedacted(exception);
    }

    [Fact]
    public async Task Pair_RejectsOversizedResponsesEvenWithoutAContentLength()
    {
        using var handler = new CallbackHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new UnknownLengthContent(new string('x', 20 * 1024))
        }));
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));

        await Assert.ThrowsAsync<AgentException>(() => api.PairAsync(Code, "0.1.0-test", CancellationToken.None));
    }

    [Fact]
    public async Task Pair_DoesNotExposeMalformedJsonOrHttpExceptionDetails()
    {
        using var malformedHandler = new CallbackHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent($"{{{Token}") }));
        using var malformedHttp = new HttpClient(malformedHandler);
        var malformedApi = new AgentApiClient(malformedHttp, ApiUri, new FixedClock(Now));

        AssertRedacted(await Assert.ThrowsAsync<AgentException>(() =>
            malformedApi.PairAsync(Code, "0.1.0-test", CancellationToken.None)));

        using var failureHandler = new CallbackHandler((_, _) => throw new HttpRequestException($"Failed request: {Code} {Token}"));
        using var failureHttp = new HttpClient(failureHandler);
        var failureApi = new AgentApiClient(failureHttp, ApiUri, new FixedClock(Now));

        AssertRedacted(await Assert.ThrowsAsync<AgentException>(() =>
            failureApi.PairAsync(Code, "0.1.0-test", CancellationToken.None)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Pair_RedactsTransportFailuresWhileReadingTheBodyAfterSuccessfulHeaders(bool httpFailure)
    {
        Exception failure = httpFailure
            ? new HttpRequestException($"Body transfer failed: {Code} {Token}")
            : new IOException($"Body stream failed: {Code} {Token}");
        using var handler = new CallbackHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new BrokenBodyContent(failure) }));
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));

        var exception = await Assert.ThrowsAsync<AgentException>(() =>
            api.PairAsync(Code, "0.1.0-test", CancellationToken.None));

        AssertRedacted(exception);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task Heartbeat_RejectsExpiredAndDifferentOriginCredentialsBeforeSendingTheToken()
    {
        using var handler = new CallbackHandler((_, _) => throw new InvalidOperationException("Unexpected request."));
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));

        await Assert.ThrowsAsync<AgentException>(() => api.HeartbeatAsync(
            Credential(expiresAtUtc: Now), "0.1.0-test", CancellationToken.None));
        await Assert.ThrowsAsync<AgentException>(() => api.HeartbeatAsync(
            Credential(apiBaseUri: new Uri("https://different.example.invalid/")), "0.1.0-test", CancellationToken.None));
    }

    [Fact]
    public async Task Heartbeat_ReportsUnauthorizedWithoutExposingCredentials()
    {
        using var handler = new CallbackHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent(Token) }));
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));

        AssertRedacted(await Assert.ThrowsAsync<AgentException>(() =>
            api.HeartbeatAsync(Credential(), "0.1.0-test", CancellationToken.None)));
    }

    [Fact]
    public async Task Pair_PropagatesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new CallbackHandler(async (_, cancellationToken) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return PairResponse();
        });
        using var http = new HttpClient(handler);
        var api = new AgentApiClient(http, ApiUri, new FixedClock(Now));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            api.PairAsync(Code, "0.1.0-test", cancellation.Token));
    }

    private static DeviceCredential Credential(Uri? apiBaseUri = null, DateTimeOffset? expiresAtUtc = null) =>
        new(apiBaseUri ?? ApiUri, DeviceId, Token, expiresAtUtc ?? Now.AddDays(90));

    private static HttpResponseMessage PairResponse(Guid? deviceId = null, string? token = null,
        DateTimeOffset? expiresAtUtc = null) => new(HttpStatusCode.Created)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            deviceId = deviceId ?? DeviceId,
            deviceToken = token ?? Token,
            expiresAtUtc = expiresAtUtc ?? Now.AddDays(90)
        }), Encoding.UTF8, "application/json")
    };

    private static void AssertRedacted(Exception exception)
    {
        Assert.False(exception.ToString().Contains(Token, StringComparison.Ordinal));
        Assert.False(exception.ToString().Contains(Code, StringComparison.Ordinal));
    }

    private sealed class CallbackHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            callback(request, cancellationToken);
    }

    private sealed class UnknownLengthContent(string content) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(Encoding.UTF8.GetBytes(content)).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class BrokenBodyContent(Exception failure) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.FromException(failure);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
