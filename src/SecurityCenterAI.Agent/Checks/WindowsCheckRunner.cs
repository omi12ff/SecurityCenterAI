using System.Globalization;
using System.Text.Json;

namespace SecurityCenterAI.Agent.Checks;

public sealed class WindowsCheckRunner
{
    private readonly TimeProvider _clock;
    private readonly IWindowsProbe _probe;

    public WindowsCheckRunner(TimeProvider clock, IWindowsProbe probe)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
    }

    public async Task<AgentReport> RunAsync(string agentVersion, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentVersion);
        var startedAt = _clock.GetUtcNow();
        var results = new List<CheckResult>(10);

        foreach (var kind in Enum.GetValues<WindowsProbeKind>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var probe = await _probe.ProbeAsync(kind, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var observedAt = _clock.GetUtcNow();
            var (id, title, risk, recommendation) = Definition(kind);
            var (status, evidence) = Evaluate(kind, probe, observedAt);
            results.Add(new CheckResult(id, 1, title,
                status == CheckStatus.Attention ? risk : "informational",
                status, evidence, recommendation, observedAt));
        }

        return new AgentReport(2, agentVersion, startedAt, _clock.GetUtcNow(), results.AsReadOnly());
    }

    private static (string Id, string Title, string Risk, string Recommendation) Definition(WindowsProbeKind kind) => kind switch
    {
        WindowsProbeKind.DefenderAntivirus => ("WIN-DEFENDER-ANTIVIRUS", "Antivirus de Microsoft Defender", "high",
            "Revisá Seguridad de Windows y confirmá qué antivirus protege el equipo. Defender puede estar desactivado por otro producto o por una política; esta regla no evalúa otros antivirus."),
        WindowsProbeKind.DefenderRealtime => ("WIN-DEFENDER-REALTIME", "Protección en tiempo real de Defender", "high",
            "Revisá la protección en tiempo real en Seguridad de Windows. Si usás otro antivirus, comprobá su estado; esta regla solo observa Defender."),
        WindowsProbeKind.DefenderSignatures => ("WIN-DEFENDER-SIGNATURES", "Antigüedad de las firmas de Defender", "medium",
            "Revisá las actualizaciones de inteligencia de seguridad de Defender. El umbral de esta regla es siete días y no verifica si existe una versión más nueva."),
        WindowsProbeKind.FirewallProfiles => ("WIN-FIREWALL-PROFILES", "Perfiles del firewall de Windows", "high",
            "Revisá los perfiles Dominio, Privado y Público del firewall de Windows. En equipos administrados, consultá al responsable antes de cambiar políticas."),
        WindowsProbeKind.SecureBoot => ("WIN-SECURE-BOOT", "Arranque seguro", "medium",
            "Revisá la compatibilidad con arranque seguro y la guía del fabricante. Conservá las claves de recuperación antes de modificar el firmware; el agente no realiza cambios."),
        WindowsProbeKind.Tpm => ("WIN-TPM-READY", "TPM presente y preparado", "medium",
            "Revisá el procesador de seguridad en Seguridad de Windows. No borres ni reinicialices el TPM; esas acciones pueden afectar las claves de cifrado."),
        WindowsProbeKind.BitLocker => ("WIN-BITLOCKER-SYSTEM", "Protección BitLocker del volumen del sistema", "medium",
            "Revisá el cifrado del dispositivo o BitLocker y la disponibilidad de tu clave de recuperación. La compatibilidad depende de la edición y del hardware; el agente no cifra ni descifra discos."),
        WindowsProbeKind.WindowsUpdateService => ("WIN-UPDATE-SERVICE", "Disponibilidad del servicio Windows Update", "medium",
            "Revisá Windows Update y sus políticas. El inicio manual puede ser normal. Esta regla observa el servicio y no determina si faltan parches o si el sistema está actualizado."),
        WindowsProbeKind.Uac => ("WIN-UAC-ENABLED", "Configuración de Control de cuentas de usuario", "medium",
            "Revisá Control de cuentas de usuario y las políticas del equipo. Esta regla solo lee EnableLUA; no evalúa el nivel de aviso ni confirma cambios pendientes de reinicio."),
        WindowsProbeKind.Smb1 => ("WIN-SMB1-SERVER", "SMB versión 1 del servidor", "high",
            "Revisá si alguna dependencia antigua requiere SMB1 y planificá su retiro. Esta regla solo consulta el componente servidor; no evalúa el cliente SMB1 ni puertos expuestos."),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static (CheckStatus, string) Evaluate(WindowsProbeKind kind, ProbeResult probe, DateTimeOffset now)
    {
        if (probe.Outcome != ProbeOutcome.Success)
        {
            return probe.Outcome switch
            {
                ProbeOutcome.NotApplicable => (CheckStatus.NotApplicable, "La comprobación no es compatible con esta plataforma; no se considera aprobada."),
                ProbeOutcome.AccessDenied => (CheckStatus.Unknown, "No se pudo consultar el estado con los permisos actuales. No se solicitaron permisos de administrador."),
                ProbeOutcome.Timeout => (CheckStatus.Unknown, "La consulta superó el tiempo máximo de 12 segundos. El estado no pudo determinarse."),
                ProbeOutcome.InvalidOutput => (CheckStatus.Unknown, "La consulta devolvió datos incompletos, inesperados o demasiado extensos."),
                _ => (CheckStatus.Unknown, "La consulta o su proveedor no está disponible. El estado no pudo determinarse.")
            };
        }

        if (string.IsNullOrWhiteSpace(probe.Json) || probe.Json.Length > 8192)
            return Invalid();

        try
        {
            using var document = JsonDocument.Parse(probe.Json, new JsonDocumentOptions { MaxDepth = 4 });
            var data = document.RootElement;
            if (data.ValueKind != JsonValueKind.Object)
                return Invalid();

            return kind switch
            {
                WindowsProbeKind.DefenderAntivirus => Antivirus(data),
                WindowsProbeKind.DefenderRealtime => Realtime(data),
                WindowsProbeKind.DefenderSignatures => Signatures(data, now),
                WindowsProbeKind.FirewallProfiles => Firewall(data),
                WindowsProbeKind.SecureBoot => Boolean(data, "enabled", true,
                    "El firmware informa arranque seguro habilitado.", "El firmware informa arranque seguro deshabilitado."),
                WindowsProbeKind.Tpm => Tpm(data),
                WindowsProbeKind.BitLocker => BitLocker(data),
                WindowsProbeKind.WindowsUpdateService => UpdateService(data),
                WindowsProbeKind.Uac => Uac(data),
                WindowsProbeKind.Smb1 => Boolean(data, "enabled", false,
                    "El componente servidor informa SMB1 deshabilitado. No se evaluó el cliente SMB1.",
                    "El componente servidor informa SMB1 habilitado. No se evaluó su exposición en la red."),
                _ => Invalid()
            };
        }
        catch (JsonException)
        {
            return Invalid();
        }
    }

    private static (CheckStatus, string) Antivirus(JsonElement data)
    {
        if (!TryBoolean(data, "antivirusEnabled", out var enabled) || !TryBoolean(data, "serviceEnabled", out var service))
            return Invalid();
        return enabled && service
            ? (CheckStatus.Passed, "Defender informa antivirus y servicio antimalware habilitados. No se evaluaron otros antivirus.")
            : (CheckStatus.Attention, "Defender informa antivirus o servicio antimalware deshabilitado. Esto no determina el estado de otros antivirus.");
    }

    private static (CheckStatus, string) Realtime(JsonElement data)
    {
        if (!TryBoolean(data, "realtimeEnabled", out var enabled))
            return Invalid();
        return enabled
            ? (CheckStatus.Passed, "Defender informa protección en tiempo real habilitada.")
            : (CheckStatus.Attention, "Defender informa protección en tiempo real deshabilitada. Otro antivirus o una política pueden explicar este estado.");
    }

    private static (CheckStatus, string) Signatures(JsonElement data, DateTimeOffset now)
    {
        if (!data.TryGetProperty("signatureUpdatedAtUtc", out var value) || value.ValueKind != JsonValueKind.String
            || !DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var updatedAt)
            || updatedAt.Offset != TimeSpan.Zero || updatedAt.Year < 2000 || updatedAt > now.AddMinutes(5))
            return Invalid();
        var age = now - updatedAt;
        var ageDays = Math.Max(0, age.TotalDays);
        var evidence = $"La fecha local de actualización de firmas tiene {ageDays.ToString("0.0", CultureInfo.InvariantCulture)} días de antigüedad. Umbral: siete días; no se consultó la última versión disponible.";
        return (age <= TimeSpan.FromDays(7) ? CheckStatus.Passed : CheckStatus.Attention, evidence);
    }

    private static (CheckStatus, string) Firewall(JsonElement data)
    {
        if (!TryBoolean(data, "domain", out var domain) || !TryBoolean(data, "private", out var privateProfile)
            || !TryBoolean(data, "public", out var publicProfile))
            return Invalid();
        return (domain && privateProfile && publicProfile ? CheckStatus.Passed : CheckStatus.Attention,
            $"Política efectiva del firewall: Dominio {Enabled(domain)}, Privado {Enabled(privateProfile)}, Público {Enabled(publicProfile)}. No se evaluaron las reglas individuales.");
    }

    private static (CheckStatus, string) Tpm(JsonElement data)
    {
        if (!TryBoolean(data, "present", out var present))
            return Invalid();
        if (!present)
            return (CheckStatus.Attention, "Windows informa que no hay un TPM presente. No se comprobó la compatibilidad del firmware.");
        if (!TryBoolean(data, "ready", out var ready))
            return Invalid();
        return ready
            ? (CheckStatus.Passed, "Windows informa un TPM presente y preparado. No se recopilaron identificadores ni claves.")
            : (CheckStatus.Attention, "Windows informa un TPM presente que no está preparado.");
    }

    private static (CheckStatus, string) BitLocker(JsonElement data)
    {
        if (!TryInteger(data, "protectionStatus", 0, 2, out var protection)
            || !TryInteger(data, "volumeStatus", 0, 5, out var volume)
            || !TryInteger(data, "encryptionPercentage", 0, 100, out var percent))
            return Invalid();
        if (protection == 2)
            return (CheckStatus.Unknown, "BitLocker informa que el estado de protección del volumen del sistema es desconocido.");
        return protection == 1 && volume == 1 && percent == 100
            ? (CheckStatus.Passed, "El volumen del sistema informa cifrado completo y protección BitLocker activa. No se recopilaron claves ni identificadores.")
            : (CheckStatus.Attention, $"El volumen del sistema no informa cifrado completo con protección activa. Protección: {(protection == 1 ? "activa" : "inactiva")}; cifrado: {percent} %. Puede estar suspendido o en transición.");
    }

    private static (CheckStatus, string) UpdateService(JsonElement data)
    {
        if (!TryString(data, "startMode", out var startMode) || !TryString(data, "state", out var state)
            || startMode is not ("Auto" or "Manual" or "Disabled")
            || state is not ("Running" or "Stopped" or "Start Pending" or "Stop Pending" or "Paused" or "Pause Pending" or "Continue Pending"))
            return Invalid();
        var modeText = startMode switch { "Auto" => "automático", "Manual" => "manual", _ => "deshabilitado" };
        var stateText = state switch { "Running" => "en ejecución", "Stopped" => "detenido", _ => "en transición o pausa" };
        return (startMode == "Disabled" ? CheckStatus.Attention : CheckStatus.Passed,
            $"Servicio Windows Update con inicio {modeText}, estado {stateText}. El inicio manual o un servicio detenido pueden ser normales; no se comprobó si faltan parches.");
    }

    private static (CheckStatus, string) Uac(JsonElement data)
    {
        if (!TryInteger(data, "enabled", 0, 1, out var enabled))
            return Invalid();
        return enabled == 1
            ? (CheckStatus.Passed, "El registro informa EnableLUA=1. No se evaluaron el nivel de aviso ni cambios pendientes de reinicio.")
            : (CheckStatus.Attention, "El registro informa EnableLUA=0. No se evaluaron cambios pendientes de reinicio.");
    }

    private static (CheckStatus, string) Boolean(JsonElement data, string name, bool expected, string passed, string attention)
    {
        if (!TryBoolean(data, name, out var actual))
            return Invalid();
        return actual == expected ? (CheckStatus.Passed, passed) : (CheckStatus.Attention, attention);
    }

    private static bool TryBoolean(JsonElement data, string name, out bool value)
    {
        value = false;
        if (!data.TryGetProperty(name, out var property) || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return false;
        value = property.GetBoolean();
        return true;
    }

    private static bool TryInteger(JsonElement data, string name, int min, int max, out int value)
    {
        value = 0;
        return data.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value) && value >= min && value <= max;
    }

    private static bool TryString(JsonElement data, string name, out string value)
    {
        value = string.Empty;
        if (!data.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return false;
        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static string Enabled(bool value) => value ? "habilitado" : "deshabilitado";

    private static (CheckStatus, string) Invalid() => (CheckStatus.Unknown,
        "La consulta no proporcionó todos los campos esperados con valores válidos; no se puede determinar el estado.");
}
