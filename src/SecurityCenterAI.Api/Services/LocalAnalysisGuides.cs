using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Domain.Rules;

namespace SecurityCenterAI.Api.Services;

/// <summary>Reviewed templates only. Client evidence never selects text, actions or links.</summary>
public static class LocalAnalysisGuides
{
    public const int Version = 1;
    private const string Defender = "https://support.microsoft.com/en-us/windows/security/threat-malware-protection/virus-and-threat-protection-in-the-windows-security-app.";
    private const string DeviceSecurity = "https://support.microsoft.com/en-us/windows/security/windows-security/device-security-in-the-windows-security-app";
    private static readonly IReadOnlyDictionary<string, (string Why, string Review, string Url)> Guides =
        new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
        {
            ["WIN-DEFENDER-ANTIVIRUS"] = ("El antivirus ayuda a detectar software malicioso.", "Abrí Seguridad de Windows → Protección contra virus y amenazas y revisá el proveedor activo.", Defender),
            ["WIN-DEFENDER-REALTIME"] = ("La protección en tiempo real examina archivos y programas mientras se utilizan.", "Abrí Seguridad de Windows → Protección contra virus y amenazas → Administrar la configuración y consultá la protección en tiempo real.", Defender),
            ["WIN-DEFENDER-SIGNATURES"] = ("La inteligencia de seguridad ayuda a reconocer amenazas recientes.", "Abrí Seguridad de Windows → Protección contra virus y amenazas → Actualizaciones de protección y consultá la fecha informada.", Defender),
            ["WIN-FIREWALL-PROFILES"] = ("El firewall filtra conexiones según las reglas y el perfil de red.", "Abrí Seguridad de Windows → Firewall y protección de red y revisá los perfiles Dominio, Privado y Público.", "https://support.microsoft.com/en-us/windows/security/windows-security/firewall-and-network-protection-in-the-windows-security-app"),
            ["WIN-SECURE-BOOT"] = ("El arranque seguro ayuda a comprobar el software que se carga al iniciar el equipo.", "Buscá Información del sistema y consultá Estado de arranque seguro. Antes de cambiar el firmware, revisá la guía del fabricante y conservá la clave de recuperación de BitLocker.", DeviceSecurity),
            ["WIN-TPM-READY"] = ("El TPM respalda funciones de seguridad que utilizan claves protegidas por hardware.", "Abrí Seguridad de Windows → Seguridad del dispositivo → Detalles del procesador de seguridad. Consultá su estado sin borrar ni reinicializar el TPM.", DeviceSecurity),
            ["WIN-BITLOCKER-SYSTEM"] = ("El cifrado ayuda a proteger datos del disco si el dispositivo se pierde o es sustraído.", "Buscá Administrar BitLocker o Cifrado del dispositivo y consultá el estado del volumen del sistema. Confirmá que disponés de la clave de recuperación antes de hacer cambios; no la compartas aquí.", "https://support.microsoft.com/en-us/windows/security/encryption/bitlocker-overview"),
            ["WIN-UPDATE-SERVICE"] = ("Windows Update permite recibir actualizaciones; el servicio por sí solo no demuestra que estén instaladas.", "Abrí Configuración → Windows Update y consultá las actualizaciones pendientes y el historial. El inicio manual del servicio puede ser normal.", "https://learn.microsoft.com/en-us/windows/deployment/update/how-windows-update-works"),
            ["WIN-UAC-ENABLED"] = ("UAC limita cambios que requieren permisos de administrador y permite revisarlos.", "Buscá Cambiar configuración de Control de cuentas de usuario y consultá el nivel de avisos. En un equipo administrado, revisalo con su responsable.", "https://learn.microsoft.com/en-us/windows/security/application-security/application-control/user-account-control/"),
            ["WIN-SMB1-SERVER"] = ("SMB1 es un protocolo antiguo con vulnerabilidades conocidas.", "Consultá con el responsable del equipo si hay aplicaciones o equipos antiguos que dependen de SMB1 antes de planificar su retiro.", "https://learn.microsoft.com/en-us/windows-server/storage/file-server/troubleshoot/detect-enable-and-disable-smbv1-v2-v3")
        };

    public static CheckExplanationResponse Explain(SecurityFinding finding, string? source)
    {
        var rule = WindowsRuleCatalog.Rules.SingleOrDefault(item => item.Id == finding.RuleId);
        if (source != "windows-agent" || finding.RuleVersion != 1 || rule is null ||
            !Guides.TryGetValue(finding.RuleId, out var guide) ||
            finding.Status is not ("passed" or "attention" or "unknown" or "notApplicable"))
            return new(finding.Id, "Este registro no tiene una interpretación local compatible.",
                "No se puede confirmar el alcance de esta versión.", "Conservamos la evidencia original sin deducir un diagnóstico.",
                "Revisá la evidencia y consultá al responsable del equipo si necesitás una evaluación.",
                "La guía requiere una regla versión 1 y un estado reconocido del agente Windows.", []);

        var meaning = finding.Status switch
        {
            "passed" => "El agente informó que este criterio se cumplía en el momento de la comprobación.",
            "attention" => "El agente informó una condición para revisar. No demuestra una infección ni confirma la causa.",
            "unknown" => "No se pudo determinar el estado. No equivale a una protección desactivada ni a un fallo confirmado.",
            _ => "El agente informó que este criterio no aplica. No suma ni resta puntos dentro de los criterios aplicables."
        };
        var action = finding.Status switch
        {
            "unknown" => "Consultá el estado manualmente; la evidencia puede indicar una limitación de permisos o compatibilidad. " + guide.Review,
            "notApplicable" => "Confirmá en la evidencia por qué no aplica. " + guide.Review,
            "passed" => "No se indica un cambio inmediato por esta comprobación. " + guide.Review,
            _ => guide.Review
        };
        return new(finding.Id, meaning, rule.Description, guide.Why, action,
            rule.Recommendation + " Es una observación autodeclarada y puntual; esta guía no modifica Windows ni garantiza seguridad completa.",
            [new OfficialReference("Documentación de Microsoft", guide.Url)]);
    }
}
