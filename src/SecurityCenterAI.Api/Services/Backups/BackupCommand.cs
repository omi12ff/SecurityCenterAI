using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SecurityCenterAI.Api.Services.Backups;

public static class BackupCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (!OperatingSystem.IsWindows()) { Console.Error.WriteLine("Este helper local requiere Windows."); return 2; }
        if (args.Length != 5 || args[0] is not ("--backup-create" or "--backup-verify") || args[1] != "--repository" ||
            args[3] != (args[0] == "--backup-create" ? "--directory" : "--file"))
        { Console.Error.WriteLine("Usá scripts/backup.ps1 -Action create o -Action verify -File <copia>."); return 2; }
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        ConsoleCancelEventHandler cancel = (_, signal) => { signal.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            Console.InputEncoding = new UTF8Encoding(false);
            var password = await Console.In.ReadLineAsync(cancellation.Token) ?? "";
            BackupEnvelope.ValidatePassword(password);
            var connection = Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSQL")
                ?? throw new InvalidOperationException("Conexión no configurada.");
            var service = new DatabaseBackupService(connection, args[2]);
            var report = args[0] == "--backup-create"
                ? await service.CreateAsync(args[4], password, cancellation.Token)
                : await service.VerifyAsync(args[4], password, cancellation.Token);
            Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            return 0;
        }
        catch (AuthenticationTagMismatchException) { Console.Error.WriteLine("La contraseña no corresponde o la copia fue alterada. No se restauró ninguna base."); return 5; }
        catch (BackupCleanupException exception) { Console.Error.WriteLine($"Revisá Docker y la base temporal {exception.Database}: no se confirmó su creación o retirada. La base habitual no es destino de restauración."); return 4; }
        catch (OperationCanceledException) { Console.Error.WriteLine("Operación cancelada o fuera de tiempo. Revisá la disponibilidad de Docker."); return 130; }
        catch (Exception) { Console.Error.WriteLine("No se completó la copia o verificación. Comprobá Docker, PostgreSQL 16, espacio, rutas y contraseña; no se imprimen datos de acceso."); return 1; }
        finally { Console.CancelKeyPress -= cancel; }
    }
}
