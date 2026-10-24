using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Api.Services.Operations;

namespace SecurityCenterAI.Tests;

public sealed class OperationalMonitoringTests
{
    private const string Secret = "NEVER_LOG_PRIVATE_MARKER_8492";

    [Fact]
    public async Task LivenessStaysAvailableWhenDatabaseIsUnhealthyAndResponsesContainNoDiagnosticDetails()
    {
        using var factory = new AccountApiFactory();
        var logs = new CapturedOperationalLogs();
        using var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureServices(services => ReplaceProbe(services, new Probe(_ => Task.FromResult(
                HealthCheckResult.Unhealthy(Secret, new InvalidOperationException(Secret),
                    new Dictionary<string, object> { ["connection"] = Secret })))));
        });
        using var client = configured.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var live = await client.GetAsync("/health");
        using var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal("healthy", (await live.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("no-store", ready.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", Assert.Single(ready.Headers.GetValues("X-Content-Type-Options")));
        var body = await ready.Content.ReadAsStringAsync();
        Assert.Equal("{\"status\":\"not_ready\",\"dependencies\":{\"postgresql\":\"unavailable\"}}", body);
        Assert.DoesNotContain(Secret, body);
        Assert.Single(logs.Entries.Where(entry => entry.Status == 503));
        AssertSafe(logs);
    }

    [Fact]
    public async Task ReadinessHealthyUsesRealProbeResultAndIsRateLimited()
    {
        using var factory = new AccountApiFactory();
        var checks = 0;
        using var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["RateLimits:ReadinessPermitLimit"] = "2" }));
            builder.ConfigureServices(services => ReplaceProbe(services, new Probe(_ =>
            {
                Interlocked.Increment(ref checks);
                return Task.FromResult(HealthCheckResult.Healthy());
            })));
        });
        using var client = configured.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var first = await client.GetAsync("/health/ready");
        using var second = await client.GetAsync("/health/ready");
        using var rejected = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("{\"status\":\"ready\",\"dependencies\":{\"postgresql\":\"available\"}}", await first.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(2, checks);
    }

    [Fact]
    public async Task ReadinessTimeoutCancelsProbeAndReturns503WithoutExceptionText()
    {
        using var factory = new AccountApiFactory();
        var cancelled = false;
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            ReplaceProbe(services, new Probe(async token =>
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                finally { cancelled = token.IsCancellationRequested; }
                return HealthCheckResult.Healthy();
            }), TimeSpan.FromMilliseconds(60))));
        using var client = configured.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(cancelled);
        Assert.DoesNotContain("exception", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnreachablePostgresProducesOnlyAnUnhealthyResultWithoutConnectingToPersonalDatabase()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PostgreSQL"] = $"Host=127.0.0.1;Port=1;Database={Secret};Username={Secret};Password={Secret};Pooling=false"
        }).Build();
        var result = await new PostgresReadinessCheck(configuration).CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Null(result.Exception);
        Assert.Null(result.Description);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task UnexpectedExceptionReturnsGenericProblemAndLogsOnlySafeFieldsEvenWithVerboseFrameworkConfiguration()
    {
        using var factory = new AccountApiFactory();
        var logs = new CapturedOperationalLogs();
        using var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Logging:LogLevel:Microsoft"] = "Trace",
                    ["Logging:LogLevel:Microsoft.AspNetCore"] = "Trace", ["Logging:LogLevel:Npgsql"] = "Trace" }));
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AccountService>();
                services.AddScoped<AccountService>(_ => throw new InvalidOperationException(Secret));
            });
        });
        using var client = configured.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/auth/login?token={Secret}");
        request.Headers.Add("X-Request-Id", Secret);
        request.Headers.Add("Cookie", $"session={Secret}");
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {Secret}");
        request.Content = JsonContent.Create(new { email = $"{Secret}@example.invalid", password = Secret });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Secret, body);
        Assert.DoesNotContain("InvalidOperationException", body);
        var requestId = Assert.Single(response.Headers.GetValues(OperationalRequestMiddleware.RequestIdHeader));
        Assert.True(Guid.TryParseExact(requestId, "N", out _));
        Assert.Equal(requestId, JsonDocument.Parse(body).RootElement.GetProperty("requestId").GetString());
        var record = Assert.Single(logs.Entries);
        Assert.Equal(1001, record.Event.Id);
        Assert.Equal(500, record.Status);
        Assert.Equal(requestId, record.Values["RequestId"]);
        Assert.Equal("api/auth/login", record.Values["Route"]?.ToString()?.TrimStart('/'));
        AssertSafe(logs);
    }

    [Fact]
    public async Task RouteValuesUnknownPathsAndUntrustedRequestIdentifiersAreExcludedFromLogs()
    {
        using var factory = new AccountApiFactory();
        var logs = new CapturedOperationalLogs();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = configured.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var deviceId = Guid.NewGuid().ToString();
        using var known = await client.GetAsync($"/api/devices/{deviceId}?private={Secret}");
        using var unknown = new HttpRequestMessage(HttpMethod.Get, $"/unknown-{Secret}");
        unknown.Headers.Add("X-Request-Id", Secret);
        using var missing = await client.SendAsync(unknown);
        Assert.Equal(HttpStatusCode.Unauthorized, known.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.NotEqual(Assert.Single(known.Headers.GetValues("X-Request-Id")), Assert.Single(missing.Headers.GetValues("X-Request-Id")));
        Assert.Equal(2, logs.Entries.Length);
        Assert.Contains(logs.Entries, entry => entry.Values["Route"]?.ToString() == "unmatched");
        Assert.DoesNotContain(deviceId, string.Join('\n', logs.Entries.Select(entry => entry.Message)));
        AssertSafe(logs);
    }

    private static void ReplaceProbe(IServiceCollection services, IHealthCheck probe, TimeSpan? timeout = null) =>
        services.Configure<HealthCheckServiceOptions>(options =>
        {
            options.Registrations.Clear();
            options.Registrations.Add(new HealthCheckRegistration("postgresql", _ => probe, HealthStatus.Unhealthy,
                ["ready"], timeout ?? TimeSpan.FromSeconds(3)));
        });

    private static void AssertSafe(CapturedOperationalLogs logs)
    {
        Assert.NotEmpty(logs.Entries);
        Assert.All(logs.Entries, entry =>
        {
            Assert.Equal(typeof(OperationalRequestMiddleware).FullName, entry.Category);
            Assert.Null(entry.Exception);
            Assert.DoesNotContain(Secret, entry.Message);
            Assert.DoesNotContain(Secret, JsonSerializer.Serialize(entry.Values));
            Assert.Equal(new[] { "ElapsedMs", "FailureCode", "Method", "RequestId", "Route", "StatusCode" },
                entry.Values.Keys.OrderBy(key => key).ToArray());
        });
    }

    private sealed class Probe(Func<CancellationToken, Task<HealthCheckResult>> check) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) => check(cancellationToken);
    }

    private sealed class CapturedOperationalLogs : ILoggerProvider
    {
        private readonly ConcurrentQueue<Entry> _entries = new();
        public Entry[] Entries => _entries.ToArray();
        public ILogger CreateLogger(string categoryName) => new CapturedLogger(categoryName, _entries);
        public void Dispose() { }

        private sealed class CapturedLogger(string category, ConcurrentQueue<Entry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var values = (state as IEnumerable<KeyValuePair<string, object?>> ?? []).Where(pair => pair.Key != "{OriginalFormat}")
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                entries.Enqueue(new Entry(category, eventId, formatter(state, exception), exception, values));
            }
        }
    }

    private sealed record Entry(string Category, EventId Event, string Message, Exception? Exception, Dictionary<string, object?> Values)
    {
        public int? Status => Values.TryGetValue("StatusCode", out var value) ? value as int? : null;
    }
}
