namespace SecurityCenterAI.Domain.Rules;

public sealed record WindowsRule(string Id, string Title, string Description, string Severity, int Weight, string Recommendation);

/// <summary>Version one is immutable: changing the meaning or weight requires a new scoring/catalog version.</summary>
public static class WindowsRuleCatalog
{
    public const int Version = 1;
    public static IReadOnlyList<WindowsRule> Rules { get; } = Array.AsReadOnly(new[]
    {
        new WindowsRule("WIN-DEFENDER-ANTIVIRUS", "Antivirus de Microsoft Defender",
            "Observa el estado declarado del antivirus y del servicio antimalware de Microsoft Defender.", "high", 15,
            "Revisá Seguridad de Windows y confirmá qué antivirus protege el equipo. Defender puede estar desactivado por otro producto o por una política; esta regla no evalúa otros antivirus."),
        new WindowsRule("WIN-DEFENDER-REALTIME", "Protección en tiempo real de Defender",
            "Observa la protección en tiempo real de Microsoft Defender.", "high", 15,
            "Revisá la protección en tiempo real en Seguridad de Windows. Si usás otro antivirus, comprobá su estado; esta regla solo observa Defender."),
        new WindowsRule("WIN-DEFENDER-SIGNATURES", "Antigüedad de las firmas de Defender",
            "Compara la fecha informada de las firmas con un umbral de siete días; no busca la última versión disponible.", "medium", 10,
            "Revisá las actualizaciones de inteligencia de seguridad de Defender. El umbral de esta regla es siete días y no verifica si existe una versión más nueva."),
        new WindowsRule("WIN-FIREWALL-PROFILES", "Perfiles del firewall de Windows",
            "Observa si los perfiles Dominio, Privado y Público del firewall están habilitados.", "high", 15,
            "Revisá los perfiles Dominio, Privado y Público del firewall de Windows. En equipos administrados, consultá al responsable antes de cambiar políticas."),
        new WindowsRule("WIN-SECURE-BOOT", "Arranque seguro",
            "Consulta el estado de arranque seguro informado por el firmware.", "medium", 10,
            "Revisá la compatibilidad con arranque seguro y la guía del fabricante. Conservá las claves de recuperación antes de modificar el firmware; el agente no realiza cambios."),
        new WindowsRule("WIN-TPM-READY", "TPM presente y preparado",
            "Consulta la presencia y preparación del TPM sin recopilar identificadores ni claves.", "medium", 5,
            "Revisá el procesador de seguridad en Seguridad de Windows. No borres ni reinicialices el TPM; esas acciones pueden afectar las claves de cifrado."),
        new WindowsRule("WIN-BITLOCKER-SYSTEM", "Protección BitLocker del volumen del sistema",
            "Observa el cifrado completo y la protección activa del volumen del sistema.", "medium", 10,
            "Revisá el cifrado del dispositivo o BitLocker y la disponibilidad de tu clave de recuperación. La compatibilidad depende de la edición y del hardware; el agente no cifra ni descifra discos."),
        new WindowsRule("WIN-UPDATE-SERVICE", "Disponibilidad del servicio Windows Update",
            "Observa el modo de inicio del servicio; no determina si faltan parches.", "medium", 5,
            "Revisá Windows Update y sus políticas. El inicio manual puede ser normal. Esta regla observa el servicio y no determina si faltan parches o si el sistema está actualizado."),
        new WindowsRule("WIN-UAC-ENABLED", "Configuración de Control de cuentas de usuario",
            "Lee EnableLUA; no evalúa el nivel de aviso ni reinicios pendientes.", "medium", 10,
            "Revisá Control de cuentas de usuario y las políticas del equipo. Esta regla solo lee EnableLUA; no evalúa el nivel de aviso ni confirma cambios pendientes de reinicio."),
        new WindowsRule("WIN-SMB1-SERVER", "SMB versión 1 del servidor",
            "Consulta si SMB1 está deshabilitado en el servidor; no evalúa el cliente ni la exposición de puertos.", "high", 5,
            "Revisá si alguna dependencia antigua requiere SMB1 y planificá su retiro. Esta regla solo consulta el componente servidor; no evalúa el cliente SMB1 ni puertos expuestos.")
    });
}
