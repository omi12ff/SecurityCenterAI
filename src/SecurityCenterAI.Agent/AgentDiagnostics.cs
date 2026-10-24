using System.Runtime.InteropServices;
using System.Security.Principal;

namespace SecurityCenterAI.Agent;

public sealed record DiagnosticEnvironment(bool IsWindows, Version OperatingSystemVersion,
    Architecture OperatingSystemArchitecture, Architecture ProcessArchitecture,
    bool SystemPowerShellAvailable, bool LocalDataLocationAvailable, bool? IsElevated, string RuntimeVersion);
public sealed record DiagnosticCheck(string Id, bool Available, string Message);
public sealed record AgentDiagnosticReport(int SchemaVersion, string AgentVersion, string OperatingSystem,
    string OperatingSystemVersion, string OperatingSystemArchitecture, string ProcessArchitecture,
    string RuntimeVersion, bool? IsElevated, bool BasicRequirementsAvailable,
    IReadOnlyList<DiagnosticCheck> Checks, IReadOnlyList<string> Limits);

/// <summary>Local prerequisites only: no network, writes, vault access or security probes.</summary>
public static class AgentDiagnostics
{
    public static AgentDiagnosticReport Capture(string agentVersion)
    {
        var windows = OperatingSystem.IsWindows();
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var nativeSystem = windows && Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess && !string.IsNullOrEmpty(system)
            ? Path.Combine(Path.GetDirectoryName(system)!, "Sysnative") : system;
        var shellAvailable = windows && !string.IsNullOrEmpty(nativeSystem) && Path.IsPathFullyQualified(nativeSystem) &&
            File.Exists(Path.Combine(nativeSystem, "WindowsPowerShell", "v1.0", "powershell.exe"));
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        bool? elevated = null;
        if (windows)
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException)
            { /* Do not expose identity or platform error messages. */ }
        }
        return Evaluate(new DiagnosticEnvironment(windows, Environment.OSVersion.Version,
            RuntimeInformation.OSArchitecture, RuntimeInformation.ProcessArchitecture, shellAvailable,
            !string.IsNullOrEmpty(localData) && Path.IsPathFullyQualified(localData), elevated,
            RuntimeInformation.FrameworkDescription), agentVersion);
    }

    public static AgentDiagnosticReport Evaluate(DiagnosticEnvironment environment, string agentVersion)
    {
        DiagnosticCheck[] checks =
        [
            new("windows-build", environment.IsWindows && environment.OperatingSystemVersion >= new Version(10, 0, 22000),
                "Windows con compilación 22000 o posterior; la matriz de pruebas se limita a Windows 11 x64."),
            new("os-x64", environment.OperatingSystemArchitecture == Architecture.X64, "Sistema operativo de arquitectura x64."),
            new("process-x64", environment.ProcessArchitecture == Architecture.X64, "Proceso del agente de arquitectura x64."),
            new("system-powershell", environment.SystemPowerShellAvailable, "Ejecutable de Windows PowerShell presente en la carpeta del sistema."),
            new("local-data-location", environment.LocalDataLocationAvailable, "Ubicación local de datos definida por Windows; no se intentó escribir en ella.")
        ];
        string[] limits =
        [
            "Este diagnóstico no consulta la API, credenciales, informes ni estados de protección y no guarda archivos.",
            "La presencia de PowerShell no confirma sus políticas, módulos o permisos de lectura. Scan informa por separado las comprobaciones desconocidas.",
            "No comprueba que la carpeta local sea escribible ni que DPAPI pueda descifrar una credencial existente.",
            "Los requisitos básicos no garantizan compatibilidad completa, un Windows actualizado ni un equipo seguro."
        ];
        return new(1, agentVersion, environment.IsWindows ? "Windows" : "Otro", environment.OperatingSystemVersion.ToString(),
            environment.OperatingSystemArchitecture.ToString(), environment.ProcessArchitecture.ToString(),
            environment.RuntimeVersion, environment.IsElevated, checks.All(check => check.Available), Array.AsReadOnly(checks), Array.AsReadOnly(limits));
    }

    public static void Print(AgentDiagnosticReport report, TextWriter output)
    {
        output.WriteLine($"Security Center AI · Diagnóstico local · Agente {report.AgentVersion}");
        output.WriteLine($"Sistema: {report.OperatingSystem} {report.OperatingSystemVersion} · {report.OperatingSystemArchitecture}");
        output.WriteLine($"Proceso: {report.ProcessArchitecture} · {report.RuntimeVersion}");
        output.WriteLine(report.IsElevated switch
        {
            true => "Esta terminal tiene permisos de administrador. Para la prueba inicial, abrí una terminal normal.",
            false => "Terminal sin elevación. El agente no solicita permisos de administrador.",
            null => "No se pudo determinar la elevación de esta terminal."
        });
        foreach (var check in report.Checks) output.WriteLine($"[{(check.Available ? "Disponible" : "No disponible")}] {check.Message}");
        output.WriteLine(report.BasicRequirementsAvailable ? "Requisitos básicos disponibles; revisá los límites antes de probar scan." :
            "Falta algún requisito básico para esta distribución de prueba (código de salida 2).");
        foreach (var limit in report.Limits) output.WriteLine(limit);
    }
}
