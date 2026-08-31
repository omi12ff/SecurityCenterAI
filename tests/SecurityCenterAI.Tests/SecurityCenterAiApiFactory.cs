using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class SecurityCenterAiApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSQL"] = "Host=unused-by-integration-tests",
                ["Jwt:Key"] = "SecurityCenterAI-integration-test-signing-key-2026",
                ["Jwt:Issuer"] = "SecurityCenterAI.Tests",
                ["Jwt:Audience"] = "SecurityCenterAI.Tests.Client",
                ["Jwt:ExpirationMinutes"] = "60",
                ["FrontendUrl"] = "http://localhost:3000",
                ["RateLimiting:RegistrationPermitLimit"] = "1000",
                ["RateLimiting:LoginPermitLimit"] = "1000"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            _connection.Open();
            services.AddDbContext<AppDbContext>(
                options => options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Database
            .EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}

public sealed class UnavailableDatabaseApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "SecurityCenterAI-missing-database-test-signing-key",
                ["Jwt:Issuer"] = "SecurityCenterAI.Tests",
                ["Jwt:Audience"] = "SecurityCenterAI.Tests.Client",
                ["Jwt:ExpirationMinutes"] = "60",
                ["ConnectionStrings:PostgreSQL"] =
                    "Host=127.0.0.1;Port=1;Database=unavailable;Username=none;Password=none;Timeout=1;Command Timeout=1"
            });
        });
    }
}
