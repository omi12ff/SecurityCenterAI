using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Api.Services.Ai;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Domain.Rules;

namespace SecurityCenterAI.Tests;

public sealed class AiExplanationProviderTests
{
    private const string FictitiousKey = "fictitious-app-key-at-least-32-chars";

    [Fact]
    public async Task Request_UsesConfiguredGatewayBearerAndFreeModel_WithClosedSingleReportInput()
    {
        var input = Input();
        using var handler = new FakeHandler((_, _) => Task.FromResult(Response(Envelope(Explanation()))));
        using var client = new HttpClient(handler);
        var result = await Provider(client).ExplainAsync(input, CancellationToken.None);
        Assert.Equal("generated", result.Status);
        Assert.NotNull(result.Explanation);
        Assert.Equal(input.Checks.Select(item => item.RuleId), result.Explanation.Checks.Select(item => item.RuleId));
        Assert.Equal(1, handler.Calls);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(new Uri("http://127.0.0.1:20128/v1/chat/completions"), handler.Uri);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", FictitiousKey), handler.Authorization);
        Assert.Contains("application/json", handler.Accept);
        using var request = JsonDocument.Parse(handler.Body!);
        var root = request.RootElement;
        Assert.Equal("openrouter/vendor/test:free", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.False(root.GetProperty("provider").GetProperty("allow_fallbacks").GetBoolean());
        Assert.True(root.GetProperty("provider").GetProperty("require_parameters").GetBoolean());
        Assert.Equal(4500, root.GetProperty("max_tokens").GetInt32());
        var format = root.GetProperty("response_format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        var structured = format.GetProperty("json_schema");
        Assert.True(structured.GetProperty("strict").GetBoolean());
        var schema = structured.GetProperty("schema");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(new[] { "summary", "checks" }, schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        var checkSchema = schema.GetProperty("properties").GetProperty("checks");
        Assert.Equal(input.Checks.Count, checkSchema.GetProperty("minItems").GetInt32());
        Assert.Equal(input.Checks.Count, checkSchema.GetProperty("maxItems").GetInt32());
        Assert.False(checkSchema.GetProperty("items").GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(input.Checks.Select(check => check.RuleId), checkSchema.GetProperty("items")
            .GetProperty("properties").GetProperty("ruleId").GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        using var report = JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);
        Assert.Equal(new[] { "score", "coveragePercent", "checks" }, report.RootElement.EnumerateObject().Select(item => item.Name));
        Assert.Equal(JsonSerializer.Serialize(input, new JsonSerializerOptions(JsonSerializerDefaults.Web)), report.RootElement.GetRawText());
        var checkFields = new[] { "ruleId", "ruleVersion", "title", "status", "meaning", "whatWasChecked", "verificationLimit" };
        foreach (var check in report.RootElement.GetProperty("checks").EnumerateArray())
            Assert.Equal(checkFields, check.EnumerateObject().Select(item => item.Name));
        Assert.DoesNotContain(FictitiousKey, handler.Body!);
        Assert.False(root.TryGetProperty("tools", out _));
        Assert.False(root.TryGetProperty("user", out _));
        Assert.False(root.TryGetProperty("conversation", out _));
    }

    [Theory]
    [InlineData("extra-root")]
    [InlineData("extra-check")]
    [InlineData("new-id")]
    [InlineData("duplicate-id")]
    [InlineData("missing-check")]
    [InlineData("missing-summary")]
    [InlineData("checks-type")]
    [InlineData("summary-type")]
    [InlineData("explanation-type")]
    [InlineData("empty-summary")]
    [InlineData("long-summary")]
    [InlineData("long-check")]
    [InlineData("unknown-confirmed")]
    [InlineData("unknown-infected")]
    [InlineData("malformed")]
    [InlineData("fenced")]
    [InlineData("html")]
    [InlineData("command")]
    [InlineData("url")]
    [InlineData("control")]
    [InlineData("large-content")]
    [InlineData("duplicate-property")]
    public async Task InvalidGeneratedContent_IsRejectedAsLocalFallback(string condition)
    {
        var explanation = Explanation();
        var checks = explanation["checks"]!.AsArray();
        var content = "";
        switch (condition)
        {
            case "extra-root": explanation["score"] = 100; break;
            case "extra-check": checks[0]!["status"] = "passed"; break;
            case "new-id": checks[0]!["ruleId"] = "UNKNOWN-RULE"; break;
            case "duplicate-id": checks[1]!["ruleId"] = checks[0]!["ruleId"]!.GetValue<string>(); break;
            case "missing-check": checks.RemoveAt(0); break;
            case "missing-summary": explanation.Remove("summary"); break;
            case "checks-type": explanation["checks"] = new JsonObject(); break;
            case "summary-type": explanation["summary"] = 100; break;
            case "explanation-type": checks[0]!["explanation"] = false; break;
            case "empty-summary": explanation["summary"] = "  "; break;
            case "long-summary": explanation["summary"] = new string('x', 2001); break;
            case "long-check": checks[0]!["explanation"] = new string('x', 1601); break;
            case "unknown-confirmed": checks[2]!["explanation"] = "Este criterio se cumple y la protección está confirmada."; break;
            case "unknown-infected": checks[2]!["explanation"] = "El equipo tiene una infección confirmada."; break;
            case "malformed": content = "{not-json"; break;
            case "fenced": content = "```json\n" + explanation.ToJsonString() + "\n```"; break;
            case "html": explanation["summary"] = "<strong>Resultado seguro</strong>"; break;
            case "command": explanation["summary"] = "Ejecutá sfc /scannow para continuar."; break;
            case "url": explanation["summary"] = "Descargá la herramienta de https://example.invalid/unknown."; break;
            case "control": explanation["summary"] = "Texto con control \u0001"; break;
            case "large-content": content = explanation.ToJsonString() + new string(' ', 25000); break;
            case "duplicate-property": content = "{\"summary\":\"uno\",\"summary\":\"dos\",\"checks\":" + checks.ToJsonString() + "}"; break;
        }
        if (content.Length == 0) content = explanation.ToJsonString();
        using var handler = new FakeHandler((_, _) => Task.FromResult(Response(Envelope(content))));
        using var client = new HttpClient(handler);
        var result = await Provider(client).ExplainAsync(Input(), CancellationToken.None);
        Assert.Equal("invalid-response", result.Status);
        Assert.Null(result.Explanation);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("missing-choices")]
    [InlineData("choices-type")]
    [InlineData("empty-choices")]
    [InlineData("multiple-choices")]
    [InlineData("message-type")]
    [InlineData("content-type")]
    [InlineData("content-null")]
    [InlineData("missing-content")]
    [InlineData("truncated")]
    [InlineData("finish-type")]
    [InlineData("tool-call")]
    [InlineData("function-call")]
    [InlineData("malformed")]
    [InlineData("root-array")]
    public async Task InvalidCompletionEnvelopes_AreRejectedWithoutRetry(string condition)
    {
        var envelope = JsonNode.Parse(Envelope(Explanation()))!.AsObject();
        var choices = envelope["choices"]!.AsArray();
        var choice = choices[0]!;
        var message = choice["message"]!.AsObject();
        switch (condition)
        {
            case "missing-choices": envelope.Remove("choices"); break;
            case "choices-type": envelope["choices"] = new JsonObject(); break;
            case "empty-choices": choices.Clear(); break;
            case "multiple-choices": choices.Add(choice.DeepClone()); break;
            case "message-type": choice["message"] = "text"; break;
            case "content-type": message["content"] = new JsonObject(); break;
            case "content-null": message["content"] = null; break;
            case "missing-content": message.Remove("content"); break;
            case "truncated": choice["finish_reason"] = "length"; break;
            case "finish-type": choice["finish_reason"] = 3; break;
            case "tool-call": message["tool_calls"] = new JsonArray(new JsonObject { ["type"] = "function" }); break;
            case "function-call": message["function_call"] = new JsonObject { ["name"] = "execute" }; break;
        }
        var body = condition switch { "malformed" => "{", "root-array" => "[]", _ => envelope.ToJsonString() };
        using var handler = new FakeHandler((_, _) => Task.FromResult(Response(body)));
        using var client = new HttpClient(handler);
        var result = await Provider(client).ExplainAsync(Input(), CancellationToken.None);
        Assert.Equal("invalid-response", result.Status);
        Assert.Null(result.Explanation);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("openrouter/vendor/test:free")]
    [InlineData("openrouter/openrouter/free")]
    public async Task ValidFreeModels_AndUnknownUncertainty_ArePreserved(string model)
    {
        var options = Configured();
        options.Model = model;
        using var handler = new FakeHandler((_, _) => Task.FromResult(Response(Envelope(Explanation()))));
        using var client = new HttpClient(handler);
        var result = await Provider(client, options).ExplainAsync(Input(), CancellationToken.None);
        Assert.Equal("generated", result.Status);
        Assert.Contains("No se pudo determinar", result.Explanation!.Checks[2].Explanation);
        Assert.Contains(model, handler.Body!);
    }

    [Theory]
    [InlineData("openrouter/auto")]
    [InlineData("openrouter/free")]
    [InlineData("openrouter/openrouter/auto")]
    [InlineData("or/vendor/model:free")]
    [InlineData("or/openrouter/free")]
    [InlineData("anthropic/vendor/model:free")]
    [InlineData("openrouter/vendor/model")]
    [InlineData("openrouter/vendor/model:paid")]
    [InlineData("openrouter/vendor/../model:free")]
    [InlineData("openrouter/vendor/model:free\n")]
    [InlineData("openrouter/free\n")]
    [InlineData("openrouter/openrouter/free\n")]
    [InlineData("or/openrouter/auto")]
    [InlineData("gpt-4o")]
    [InlineData("or/vendor/paid")]
    [InlineData("or/vendor/model:paid")]
    [InlineData("or/vendor/../model:free")]
    [InlineData("or/vendor/model:free\n")]
    public async Task NonFreeOrAmbiguousModelConfiguration_MakesNoRequest(string model)
    {
        var options = Configured();
        options.Model = model;
        using var handler = new FakeHandler((_, _) => throw new InvalidOperationException("No network request expected."));
        using var client = new HttpClient(handler);
        Assert.False(options.IsConfigured());
        var result = await Provider(client, options).ExplainAsync(Input(), CancellationToken.None);
        Assert.Equal("unavailable", result.Status);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("http://remote.example.invalid/v1/")]
    [InlineData("http://localhost.example.invalid/v1/")]
    [InlineData("http://127.0.0.1:20128/v2/")]
    [InlineData("http://user:pass@127.0.0.1:20128/v1/")]
    [InlineData("http://127.0.0.1:20128/v1/?secret=value")]
    [InlineData("http://127.0.0.1:20128/v1/#other")]
    public async Task InvalidGatewayConfiguration_MakesNoRequest(string gateway)
    {
        var options = Configured();
        options.GatewayBaseUrl = gateway;
        using var handler = new FakeHandler((_, _) => throw new InvalidOperationException("No request expected."));
        using var client = new HttpClient(handler);
        Assert.False(options.IsConfigured());
        var result = await Provider(client, options).ExplainAsync(Input(), CancellationToken.None);
        Assert.Equal("unavailable", result.Status);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("no-model")]
    [InlineData("no-key")]
    public async Task DisabledOrUnconfiguredProvider_MakesNoRequest(string condition)
    {
        var options = Configured();
        if (condition == "disabled") options.Enabled = false;
        if (condition == "no-model") options.Model = "";
        if (condition == "no-key") options.ApiKey = "";
        using var handler = new FakeHandler((_, _) => throw new InvalidOperationException("No request expected."));
        using var client = new HttpClient(handler);
        var result = await Provider(client, options).ExplainAsync(Input(), CancellationToken.None);
        Assert.Equal("unavailable", result.Status);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyAndOversizedResponses_AreBoundedForDeclaredAndChunkedBodies(bool declared)
    {
        var content = new TrackedContent(new byte[70_000], declared);
        using var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var client = new HttpClient(handler);
        var result = await Provider(client).ExplainAsync(Input(), CancellationToken.None);
        Assert.Equal("invalid-response", result.Status);
        Assert.Equal(1, handler.Calls);
        if (declared) Assert.Equal(0, content.Stream.TotalRead);
        else Assert.InRange(content.Stream.TotalRead, 65_537, 65_536 + 8192);

        using var emptyHandler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new TrackedContent([], declared) }));
        using var emptyClient = new HttpClient(emptyHandler);
        var empty = await Provider(emptyClient).ExplainAsync(Input(), CancellationToken.None);
        Assert.Equal("invalid-response", empty.Status);
        Assert.Equal(1, emptyHandler.Calls);
    }

    [Theory]
    [InlineData(400, "gateway-request-rejected")]
    [InlineData(401, "provider-authentication")]
    [InlineData(402, "provider-budget")]
    [InlineData(403, "provider-model-denied")]
    [InlineData(404, "provider-unavailable")]
    [InlineData(408, "timeout")]
    [InlineData(429, "provider-rate-limited")]
    [InlineData(503, "provider-unavailable")]
    [InlineData(502, "provider-unavailable")]
    [InlineData(504, "timeout")]
    [InlineData(302, "provider-unavailable")]
    public async Task HttpFailuresAndRedirects_ReturnUnavailable_WithoutRetryOrSecondRequest(int status, string expectedFailure)
    {
        using var handler = new FakeHandler((_, _) =>
        {
            var response = new HttpResponseMessage((HttpStatusCode)status);
            response.Content = new StringContent("{\"error\":\"PRIVATE-UPSTREAM-DETAILS\"}");
            response.Headers.Location = new Uri("https://other.example.invalid/v1/chat/completions");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);
        var logger = new CapturingLogger();
        var provider = new OmniRouteExplanationProvider(client, Options.Create(Configured()), new AiExplanationCredential(), logger);
        var result = await provider.ExplainAsync(Input(), CancellationToken.None);
        Assert.Equal("unavailable", result.Status);
        Assert.Equal(expectedFailure, result.FailureCode);
        Assert.Equal($"AI explanation unavailable: {expectedFailure}; gateway HTTP status: {status}.", Assert.Single(logger.Entries));
        Assert.DoesNotContain("PRIVATE-UPSTREAM-DETAILS", string.Join("", logger.Entries));
        Assert.DoesNotContain(FictitiousKey, string.Join("", logger.Entries));
        Assert.DoesNotContain("other.example.invalid", string.Join("", logger.Entries));
        Assert.DoesNotContain("PRIVATE-UPSTREAM-DETAILS", JsonSerializer.Serialize(result));
        Assert.Null(result.Explanation);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("http")]
    [InlineData("timeout")]
    public async Task TransportExceptions_ReturnUnavailable_WithoutRetry(string condition)
    {
        using var handler = new FakeHandler((_, _) => condition switch
        {
            "io" => throw new IOException("PRIVATE-UPSTREAM-DETAILS"),
            "http" => throw new HttpRequestException("PRIVATE-UPSTREAM-DETAILS"),
            _ => throw new TaskCanceledException("PRIVATE-UPSTREAM-DETAILS")
        });
        using var client = new HttpClient(handler);
        var logger = new CapturingLogger();
        var provider = new OmniRouteExplanationProvider(client, Options.Create(Configured()), new AiExplanationCredential(), logger);
        var result = await provider.ExplainAsync(Input(), CancellationToken.None);
        Assert.Equal("unavailable", result.Status);
        Assert.Null(result.Explanation);
        Assert.Equal(1, handler.Calls);
        Assert.Single(logger.Entries);
        Assert.DoesNotContain("PRIVATE-UPSTREAM-DETAILS", string.Join("", logger.Entries));
        Assert.DoesNotContain(FictitiousKey, string.Join("", logger.Entries));
    }

    [Fact]
    public async Task ProviderDeadline_CancelsTheSingleRequest_AndReturnsLocalFallback()
    {
        using var handler = new FakeHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The provider deadline must cancel the request.");
        });
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var options = Configured();
        options.TimeoutSeconds = 5;
        var result = await Provider(client, options).ExplainAsync(Input(), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("unavailable", result.Status);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CallerCancellation_PropagatesWithoutRetryOrFallback()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new FakeHandler(async (_, cancellationToken) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The caller should cancel this request.");
        });
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        var pending = Provider(client).ExplainAsync(Input(), cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(1, handler.Calls);
    }

    private static OmniRouteExplanationProvider Provider(HttpClient client, AiExplanationOptions? options = null) =>
        new(client, Options.Create(options ?? Configured()), new AiExplanationCredential());

    [Fact]
    public async Task RejectedOutput_LogsOnlyFixedReasonWithoutContentOrCredential()
    {
        var explanation = Explanation();
        explanation["summary"] = "NEVER_LOG_MODEL_CONTENT <script>";
        using var handler = new FakeHandler((_, _) => Task.FromResult(Response(Envelope(explanation))));
        using var client = new HttpClient(handler);
        var logger = new CapturingLogger();
        var provider = new OmniRouteExplanationProvider(client, Options.Create(Configured()), new AiExplanationCredential(), logger);
        var result = await provider.ExplainAsync(Input(), CancellationToken.None);
        Assert.Equal("invalid-response", result.Status);
        Assert.Null(result.Explanation);
        Assert.Equal("AI explanation rejected: summary-shape-or-content", Assert.Single(logger.Entries));
        Assert.DoesNotContain(FictitiousKey, string.Join("", logger.Entries));
        Assert.DoesNotContain("NEVER_LOG_MODEL_CONTENT", string.Join("", logger.Entries));
    }

    private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger<OmniRouteExplanationProvider>
    {
        public List<string> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel level, Microsoft.Extensions.Logging.EventId id,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Entries.Add(formatter(state, exception));
    }

    private static AiExplanationOptions Configured() => new()
    {
        Enabled = true, GatewayBaseUrl = "http://127.0.0.1:20128/v1/", Model = "openrouter/vendor/test:free", ApiKey = FictitiousKey
    };

    private static AiExplanationInput Input() => new(75, 70, WindowsRuleCatalog.Rules.Select((rule, index) =>
    {
        var finding = new SecurityFinding
        {
            RuleId = rule.Id, RuleVersion = 1, Status = index == 2 ? "unknown" : "passed",
            Title = "PRIVATE-STORED-TITLE", Evidence = "PRIVATE-RAW-EVIDENCE", Recommendation = "PRIVATE-RECOMMENDATION", Severity = "medium"
        };
        var local = LocalAnalysisGuides.Explain(finding, "windows-agent");
        return new AiExplanationInputCheck(rule.Id, 1, rule.Title, finding.Status, local.Meaning, local.WhatWasChecked, local.VerificationLimit);
    }).ToArray());

    private static JsonObject Explanation() => new()
    {
        ["summary"] = "Guía para comprender las observaciones puntuales del informe.",
        ["checks"] = new JsonArray(WindowsRuleCatalog.Rules.Select(rule => (JsonNode)new JsonObject
        {
            ["ruleId"] = rule.Id,
            ["explanation"] = "No se pudo determinar una conclusión fuera del alcance de esta comprobación. Consultá la guía local."
        }).ToArray())
    };

    private static string Envelope(JsonObject explanation) => Envelope(explanation.ToJsonString());
    private static string Envelope(string content) => JsonSerializer.Serialize(new
    {
        choices = new[] { new { finish_reason = "stop", message = new { role = "assistant", content, tool_calls = (object?)null, function_call = (object?)null } } }
    });

    private static HttpResponseMessage Response(string body) => new(HttpStatusCode.OK)
    { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpMethod? Method { get; private set; }
        public Uri? Uri { get; private set; }
        public AuthenticationHeaderValue? Authorization { get; private set; }
        public string Accept { get; private set; } = "";
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Method = request.Method;
            Uri = request.RequestUri;
            Authorization = request.Headers.Authorization;
            Accept = request.Headers.Accept.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await respond(request, cancellationToken);
        }
    }

    private sealed class TrackedContent(byte[] payload, bool declared) : HttpContent
    {
        public TrackedStream Stream { get; } = new(payload);
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(payload).AsTask();
        protected override bool TryComputeLength(out long length) { length = payload.Length; return declared; }
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(Stream);
    }

    private sealed class TrackedStream(byte[] payload) : MemoryStream(payload)
    {
        public int TotalRead { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = await base.ReadAsync(buffer, cancellationToken);
            TotalRead += count;
            return count;
        }
    }
}
