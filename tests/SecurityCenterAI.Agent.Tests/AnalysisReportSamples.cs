using SecurityCenterAI.Agent.Checks;

namespace SecurityCenterAI.Agent.Tests;

internal static class AnalysisReportSamples
{
    public static readonly string[] RuleIds =
    [
        "WIN-DEFENDER-ANTIVIRUS", "WIN-DEFENDER-REALTIME", "WIN-DEFENDER-SIGNATURES",
        "WIN-FIREWALL-PROFILES", "WIN-SECURE-BOOT", "WIN-TPM-READY", "WIN-BITLOCKER-SYSTEM",
        "WIN-UPDATE-SERVICE", "WIN-UAC-ENABLED", "WIN-SMB1-SERVER"
    ];

    public static AgentReport Create(DateTimeOffset now) => new(2, "0.2.0-contract-test", now.AddSeconds(-10), now,
        RuleIds.Select(ruleId => new CheckResult(ruleId, 1, "Local title must not be submitted", "critical",
            CheckStatus.Passed, "Evidencia sintética de una prueba aislada.", "Local advice must not be submitted", now))
            .ToArray());
}
