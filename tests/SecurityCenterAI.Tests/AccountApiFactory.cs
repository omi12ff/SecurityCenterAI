using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

internal sealed class AccountApiFactory(int authPermitLimit = 1000, int globalPermitLimit = 2000,
    int agentPairPermitLimit = 1000, int devicePermitLimit = 1000, int analysisPermitLimit = 1000)
    : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    public TestClock Clock { get; } = new();
    public CapturedAccountEmailSender Email { get; } = new();
    public AuthSecurityOptions SecurityOptions => Services.GetRequiredService<IOptions<AuthSecurityOptions>>().Value;
    public DeviceSecurityOptions DeviceOptions => Services.GetRequiredService<IOptions<DeviceSecurityOptions>>().Value;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["RateLimits:AuthPermitLimit"] = authPermitLimit.ToString(),
                ["RateLimits:EmailPermitLimit"] = "1000",
                ["RateLimits:GlobalPermitLimit"] = globalPermitLimit.ToString(),
                ["RateLimits:AgentPairPermitLimit"] = agentPairPermitLimit.ToString(),
                ["RateLimits:DevicePermitLimit"] = devicePermitLimit.ToString(),
                ["RateLimits:AnalysisPermitLimit"] = analysisPermitLimit.ToString()
            }));
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IAccountEmailSender>();
            services.AddSingleton<IAccountEmailSender>(Email);
        });
    }

    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost")
    });

    public async Task<AccountAuthentication> RegisterAsync(HttpClient client, AccountCredentials account)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/register",
            new { account.Name, account.Email, account.Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var authentication = await response.Content.ReadFromJsonAsync<AccountAuthentication>();
        Assert.NotNull(authentication);
        Assert.True(!string.IsNullOrWhiteSpace(authentication.AccessToken));
        Assert.True(!string.IsNullOrWhiteSpace(authentication.RefreshToken));
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(body.Contains(account.Password, StringComparison.Ordinal));
        Assert.False(body.Contains("passwordHash", StringComparison.OrdinalIgnoreCase));
        foreach (var message in Email.Messages.Where(message => message.Recipient == account.Email))
            Assert.False(body.Contains(message.Token, StringComparison.Ordinal));
        return authentication;
    }

    public static HttpRequestMessage AuthorizedRequest(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}

internal sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan duration) => _now = _now.Add(duration);
}

internal sealed class CapturedAccountEmailSender : IAccountEmailSender
{
    private readonly ConcurrentQueue<AccountEmail> _messages = new();
    public AccountEmail[] Messages => _messages.ToArray();

    public Task SendAsync(AccountEmail message, CancellationToken cancellationToken)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public string TokenFor(string recipient, AccountEmailPurpose purpose)
    {
        var message = _messages.LastOrDefault(item => item.Recipient == recipient && item.Purpose == purpose);
        Assert.True(message is not null, "The isolated email sender should receive the requested account message.");
        return message!.Token;
    }
}

internal sealed record AccountCredentials(string Name, string Email, string Password)
{
    public static AccountCredentials New() => new("Usuario de pruebas",
        $"account-{Guid.NewGuid():N}@example.invalid", Convert.ToHexString(RandomNumberGenerator.GetBytes(24)));
}

internal sealed record AccountAuthentication(
    string AccessToken, DateTime ExpiresAtUtc, AccountUser User,
    string RefreshToken, DateTime RefreshExpiresAtUtc);

internal sealed record AccountUser(Guid Id, string Name, string Email, bool EmailVerified);
