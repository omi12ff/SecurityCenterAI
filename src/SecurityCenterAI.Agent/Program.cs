using System.Reflection;
using System.Text.Json;
using SecurityCenterAI.Agent.Checks;
using SecurityCenterAI.Agent.Security;
using SecurityCenterAI.Agent.Transport;

namespace SecurityCenterAI.Agent;

public static class Program
{
    public static string AgentVersion { get; } = typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        try
        {
            if (args.Length == 0 || args is ["help"] || args is ["--help"])
            {
                PrintHelp();
                return 0;
            }
            if (args is ["version"] or ["--version"])
            {
                Console.WriteLine(AgentVersion);
                return 0;
            }
            var command = ParseArguments(args);
            // Diagnostics must remain independent of user data and the vault lock.
            if (command.Action == "doctor")
            {
                var diagnostics = AgentDiagnostics.Capture(AgentVersion);
                if (command.Json) Console.WriteLine(JsonSerializer.Serialize(diagnostics, new JsonSerializerOptions
                { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
                else AgentDiagnostics.Print(diagnostics, Console.Out);
                return diagnostics.BasicRequirementsAvailable ? 0 : 2;
            }
            if (!OperatingSystem.IsWindows()) throw new AgentException("Este agente solo funciona en Windows.");
            var clock = TimeProvider.System;
            var storage = AgentStorageDirectory.Resolve(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            var vault = new CredentialVault(storage, clock);
            using var operationLock = vault.AcquireLock();
            var ct = cancellation.Token;
            switch (command.Action)
            {
                case "pair":
                    if (File.Exists(vault.CredentialPath) && !command.Replace)
                        throw new AgentException("Ya hay una credencial local. Usá status, o pair --replace si querés sustituirla con un código nuevo.");
                    var endpoint = ApiEndpoint.Parse(command.ApiUrl!);
                    Console.WriteLine($"Vinculando con {endpoint.GetLeftPart(UriPartial.Authority)}");
                    if (command.Replace) Console.WriteLine("Se sustituirá la credencial local. El código determina qué dispositivo se vincula.");
                    Console.Write("Pegá el código de Mis dispositivos (entrada oculta): ");
                    var code = await ReadSecretAsync(ct);
                    using (var http = AgentApiClient.CreateHttpClient())
                    {
                        var client = new AgentApiClient(http, endpoint, clock);
                        var paired = await client.PairAsync(code, AgentVersion, ct);
                        // From here the code is consumed. A failed save requires issuing a new code.
                        await vault.SaveAsync(paired, ct);
                        Console.WriteLine("Equipo vinculado. Credencial cifrada para tu usuario de Windows.");
                        await client.HeartbeatAsync(paired, AgentVersion, ct);
                        Console.WriteLine("Contacto confirmado. Actualizá Mis dispositivos en la web.");
                    }
                    break;
                case "heartbeat":
                    var credential = await vault.LoadAsync(ct) ?? throw Unpaired();
                    using (var http = AgentApiClient.CreateHttpClient())
                        await new AgentApiClient(http, credential.ApiBaseUri, clock).HeartbeatAsync(credential, AgentVersion, ct);
                    Console.WriteLine("Contacto confirmado. Actualizá Mis dispositivos en la web.");
                    break;
                case "status":
                    var current = await vault.LoadAsync(ct);
                    if (current is null) Console.WriteLine("Sin credencial local. Usá pair para vincular el equipo.");
                    else
                    {
                        Console.WriteLine($"Dispositivo: {current.DeviceId}");
                        Console.WriteLine($"API: {current.ApiBaseUri.GetLeftPart(UriPartial.Authority)}");
                        Console.WriteLine($"Credencial local válida hasta: {current.ExpiresAtUtc:u}");
                        Console.WriteLine("Este estado es local. Usá heartbeat para comprobar el acceso con la API.");
                    }
                    break;
                case "forget":
                    await vault.ForgetAsync(ct);
                    Console.WriteLine("Credencial local eliminada. Esto no revoca el acceso remoto ni borra informes.");
                    Console.WriteLine("Para invalidar todas sus copias, revocá el equipo desde Mis dispositivos.");
                    break;
                case "scan":
                    // Check local linkage before scanning when the user explicitly requested upload.
                    var scanCredential = command.Send ? await vault.LoadAsync(ct) ?? throw Unpaired() : null;
                    Console.WriteLine("Comprobando 10 configuraciones de Windows en modo de solo lectura...");
                    Console.WriteLine(command.Send ? "Se guardará el informe local y se enviarán las diez comprobaciones a la API vinculada." : "El informe se guarda localmente. Para enviarlo después, usá send.");
                    var report = await new WindowsCheckRunner(clock, new PowerShellWindowsProbe()).RunAsync(AgentVersion, ct);
                    foreach (var check in report.Checks)
                        Console.WriteLine($"[{StatusLabel(check.Status)}] {check.Title}: {check.Evidence}");
                    var reportPath = await LocalReportWriter.WriteAsync(storage, report, ct);
                    Console.WriteLine($"Informe JSON: {reportPath}");
                    Console.WriteLine("Los resultados desconocidos requieren comprobación manual.");
                    if (scanCredential is not null) await SendReportAsync(scanCredential, report, clock, ct);
                    break;
                case "send":
                    var sendCredential = await vault.LoadAsync(ct) ?? throw Unpaired();
                    var latest = await LocalReportReader.ReadLatestAsync(storage, ct);
                    await SendReportAsync(sendCredential, latest, clock, ct);
                    break;
            }
            return 0;
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("Operación cancelada."); return 130; }
        catch (AgentException exception) { Console.Error.WriteLine(exception.Message); return 1; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            System.Security.Cryptography.CryptographicException)
        {
            Console.Error.WriteLine("No se pudo guardar o leer el estado local. Revisá los permisos del usuario de Windows.");
            Console.Error.WriteLine("Si acababas de vincular, generá un código nuevo antes de reintentar.");
            return 1;
        }
    }

    private static AgentCommand ParseArguments(string[] args)
    {
        var action = args[0];
        if (args is ["doctor"]) return new AgentCommand(action);
        if (args is ["doctor", "--json"]) return new AgentCommand(action, Json: true);
        if (action == "scan" && args is ["scan", "--send"]) return new AgentCommand(action, Send: true);
        if (action is "scan" or "send" or "status" or "heartbeat" or "forget" && args.Length == 1)
            return new AgentCommand(action);
        if (action != "pair") throw UsageError();
        string? apiUrl = null;
        var replace = false;
        for (var index = 1; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--api" when apiUrl is null && index + 1 < args.Length:
                    apiUrl = args[++index];
                    break;
                case "--replace" when !replace:
                    replace = true;
                    break;
                default: throw UsageError();
            }
        }
        return new AgentCommand(action, apiUrl ?? "http://localhost:5169", replace);
    }

    private static async Task<string> ReadSecretAsync(CancellationToken cancellationToken)
    {
        if (Console.IsInputRedirected)
            throw new AgentException("La vinculación requiere una terminal interactiva para ocultar el código. Abrí PowerShell o la terminal de VS Code.");
        var value = new List<char>(43);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Console.KeyAvailable)
            {
                await Task.Delay(50, cancellationToken);
                continue;
            }
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return new string(value.ToArray()).Trim(); }
            if (key.Key == ConsoleKey.Escape) throw new OperationCanceledException(cancellationToken);
            if (key.Key == ConsoleKey.Backspace) { if (value.Count > 0) value.RemoveAt(value.Count - 1); continue; }
            if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar))
            {
                if (value.Count >= 256) throw new AgentException("El código ingresado es demasiado largo. Generá uno nuevo y copialo completo.");
                value.Add(key.KeyChar);
            }
        }
    }

    private static string StatusLabel(CheckStatus status) => status switch
    {
        CheckStatus.Passed => "Correcto",
        CheckStatus.Attention => "Revisar",
        CheckStatus.NotApplicable => "No aplica",
        _ => "Desconocido"
    };

    private static AgentException Unpaired() => new("No hay credencial local. Registrá el equipo en la web y usá pair con un código nuevo.");
    private static AgentException UsageError() => new("Comando u opciones inválidas. Usá help; el código de vinculación nunca se pasa como argumento.");
    private sealed record AgentCommand(string Action, string? ApiUrl = null, bool Replace = false, bool Send = false, bool Json = false);

    private static async Task SendReportAsync(DeviceCredential credential, AgentReport report, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        using var http = AgentApiClient.CreateHttpClient();
        var receipt = await new AgentApiClient(http, credential.ApiBaseUri, clock)
            .SubmitAnalysisAsync(credential, report, cancellationToken);
        Console.WriteLine(receipt.AlreadyReceived ? "El informe ya estaba guardado; no se duplicó." : "Informe guardado en la cuenta del equipo vinculado.");
        Console.WriteLine($"Puntos confirmados: {(receipt.Score.HasValue ? $"{receipt.Score}/100" : "sin puntaje verificable")}. Cobertura: {receipt.CoveragePercent}%.");
        Console.WriteLine($"Correctas: {receipt.PassedCount}; revisar: {receipt.AttentionCount}; desconocidas: {receipt.UnknownCount}; no aplicables: {receipt.NotApplicableCount}.");
        Console.WriteLine("Actualizá el resumen o el historial del equipo en la web para ver los resultados.");
    }

    private static void PrintHelp()
    {
        Console.WriteLine($"Security Center AI · Agente Windows {AgentVersion}");
        Console.WriteLine("pair [--api URL] [--replace]  Vincula con un código ingresado de forma oculta.");
        Console.WriteLine("heartbeat                    Confirma contacto con la API guardada.");
        Console.WriteLine("status                       Consulta la credencial local sin mostrar secretos.");
        Console.WriteLine("scan [--send]                Comprueba Windows, guarda JSON y opcionalmente lo envía.");
        Console.WriteLine("send                         Envía el último informe compatible; reintentar no lo duplica.");
        Console.WriteLine("forget                       Elimina solo la credencial local; revocá desde la web.");
        Console.WriteLine("doctor [--json]              Diagnóstico local básico sin leer credenciales ni ejecutar scan.");
        Console.WriteLine("version                      Muestra la versión del agente; también admite --version.");
        Console.WriteLine("help                         Muestra esta ayuda.");
        Console.WriteLine("Uso manual. El envío requiere send o scan --send. No instala servicios ni corrige configuraciones.");
    }
}
