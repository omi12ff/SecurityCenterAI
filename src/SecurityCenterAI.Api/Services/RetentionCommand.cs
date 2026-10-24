using System.Text.Json;
using Microsoft.Extensions.Options;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Infrastructure;

namespace SecurityCenterAI.Api.Services;

public static class RetentionCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var preview = args.Contains("--retention-preview");
        var apply = args.Contains("--retention-apply");
        if (preview == apply || args.Distinct().Count() != args.Length ||
            args.Any(arg => arg is not ("--retention-preview" or "--retention-apply" or "--confirm-retention")) ||
            (apply != args.Contains("--confirm-retention")))
        { Console.Error.WriteLine("Usá --retention-preview, o --retention-apply --confirm-retention con la purga habilitada."); return 2; }
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, signal) => { signal.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            // No web host, SMTP worker, JWT setup, migrations or files are started.
            var builder = Host.CreateApplicationBuilder(Array.Empty<string>());
            builder.Logging.ClearProviders();
            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.AddOptions<RetentionOptions>().Bind(builder.Configuration.GetSection(RetentionOptions.SectionName))
                .Validate(RetentionOptions.Valid, "Configuración de retención inválida.");
            builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
            builder.Services.AddScoped<RetentionMaintenanceService>();
            using var host = builder.Build();
            await using var scope = host.Services.CreateAsyncScope();
            var report = await scope.ServiceProvider.GetRequiredService<RetentionMaintenanceService>().RunAsync(apply, cancellation.Token);
            Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            return report.Status == "busy" ? 3 : 0;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { Console.Error.WriteLine("Mantenimiento cancelado."); return 130; }
        catch (Exception) { Console.Error.WriteLine("No se completó el mantenimiento. Revisá la conexión y configuración; no se muestran datos de acceso."); return 1; }
        finally { Console.CancelKeyPress -= cancel; }
    }
}
