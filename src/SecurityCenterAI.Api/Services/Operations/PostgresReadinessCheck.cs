using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace SecurityCenterAI.Api.Services.Operations;

public sealed class PostgresReadinessCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Read-only probe; no DbContext, migrations, user rows or SMTP messages.
            var settings = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("PostgreSQL"))
            { Timeout = 3, CommandTimeout = 3, ApplicationName = "SecurityCenterAI.Readiness" };
            await using var connection = new NpgsqlConnection(settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is 1 ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // Health reports must never carry provider exceptions or connection data.
            return HealthCheckResult.Unhealthy();
        }
    }
}
