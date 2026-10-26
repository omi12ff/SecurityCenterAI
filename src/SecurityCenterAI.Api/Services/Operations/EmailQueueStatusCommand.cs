using System.Globalization;
using System.Text.Json;

namespace SecurityCenterAI.Api.Services.Operations;

public static class EmailQueueStatusCommand
{
    public static bool TryParse(string[] args, out EmailQueueThresholds thresholds)
    {
        thresholds = new();
        if (args.Length == 0 || args[0] != "--email-queue-status" || args.Length % 2 != 1) return false;
        var count = thresholds.WarningCount; var age = thresholds.WarningAgeMinutes;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            if (!seen.Add(args[index]) || !int.TryParse(args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var value)) return false;
            switch (args[index])
            {
                case "--warning-count": count = value; break;
                case "--warning-age-minutes": age = value; break;
                default: return false;
            }
        }
        thresholds = new(count, age);
        try { thresholds.Validate(); return true; }
        catch (ArgumentOutOfRangeException) { return false; }
    }

    public static async Task<int> RunAsync(string[] args)
    {
        if (!TryParse(args, out var thresholds))
        { Console.Error.WriteLine("Usá --email-queue-status [--warning-count 1..1000000] [--warning-age-minutes 1..10080]."); return 2; }
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        ConsoleCancelEventHandler cancel = (_, signal) => { signal.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            // No web host, worker, migrations, encryption keys, SMTP or payload reads.
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environments.Production;
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [], EnvironmentName = environment });
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
            builder.Services.AddScoped<EmailQueueStatusReader>();
            using var host = builder.Build();
            await using var scope = host.Services.CreateAsyncScope();
            var report = await scope.ServiceProvider.GetRequiredService<EmailQueueStatusReader>().ReadAsync(thresholds, cancellation.Token);
            Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            return report.Status == "attention" ? 3 : 0;
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("Consulta de cola cancelada o fuera de tiempo."); return 130; }
        catch (Exception) { Console.Error.WriteLine("No se pudo consultar la cola. Revisá conexión, permisos, configuración y migración; no se muestran datos de acceso."); return 1; }
        finally { Console.CancelKeyPress -= cancel; }
    }
}
