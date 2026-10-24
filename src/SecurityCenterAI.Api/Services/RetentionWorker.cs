using Microsoft.Extensions.Options;
using SecurityCenterAI.Api.Configuration;

namespace SecurityCenterAI.Api.Services;

public sealed class RetentionWorker(IServiceScopeFactory scopes, IOptions<RetentionOptions> configured,
    TimeProvider clock, ILogger<RetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configured.Value.Enabled) return;
        try
        {
            // Enabling the worker never purges during startup; the first run waits
            // a full interval. Manual preview/apply is available independently.
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromHours(configured.Value.IntervalHours), clock, stoppingToken);
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var report = await scope.ServiceProvider.GetRequiredService<RetentionMaintenanceService>().RunAsync(true, stoppingToken);
                    logger.LogInformation("Mantenimiento técnico: {Status}; sesiones {Sessions}, tokens {Tokens}, códigos {Codes}, credenciales {Credentials}.",
                        report.Status, report.Removed.Sessions, report.Removed.AccountTokens, report.Removed.PairingCodes, report.Removed.DeviceCredentials);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception) { logger.LogWarning("No se completó el mantenimiento técnico. Revisá la disponibilidad y configuración antes de repetirlo."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
