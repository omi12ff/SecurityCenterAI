using SecurityCenterAI.Agent.Checks;

namespace SecurityCenterAI.Agent.Transport;

public sealed record AnalysisReceipt(Guid AnalysisId, Guid ReportId, int? Score, int CoveragePercent,
    int PassedCount, int AttentionCount, int UnknownCount, int NotApplicableCount,
    int ScoringVersion, DateTimeOffset ReceivedAtUtc, bool AlreadyReceived);

public sealed record AnalysisSubmission(Guid ReportId, int SchemaVersion, string AgentVersion,
    DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc, IReadOnlyList<SubmittedCheck> Checks);

public sealed record SubmittedCheck(string RuleId, int RuleVersion, string Status, string Evidence,
    DateTimeOffset ObservedAtUtc);

public static class ReportSubmission
{
    private static readonly HashSet<string> RuleIds = new(StringComparer.Ordinal)
    {
        "WIN-DEFENDER-ANTIVIRUS", "WIN-DEFENDER-REALTIME", "WIN-DEFENDER-SIGNATURES",
        "WIN-FIREWALL-PROFILES", "WIN-SECURE-BOOT", "WIN-TPM-READY", "WIN-BITLOCKER-SYSTEM",
        "WIN-UPDATE-SERVICE", "WIN-UAC-ENABLED", "WIN-SMB1-SERVER"
    };

    public static AnalysisSubmission Create(AgentReport report)
    {
        if (report.SchemaVersion != 2 || report.ReportId == Guid.Empty || report.Checks is null ||
            report.Checks.Count != RuleIds.Count || string.IsNullOrWhiteSpace(report.AgentVersion) ||
            report.AgentVersion.Length > 64 || report.AgentVersion.Any(char.IsControl)) throw Invalid();
        if (report.StartedAtUtc.Offset != TimeSpan.Zero || report.CompletedAtUtc.Offset != TimeSpan.Zero ||
            report.StartedAtUtc > report.CompletedAtUtc || report.CompletedAtUtc - report.StartedAtUtc > TimeSpan.FromMinutes(30))
            throw Invalid();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var checks = new List<SubmittedCheck>(RuleIds.Count);
        foreach (var check in report.Checks)
        {
            if (check is null || !RuleIds.Contains(check.RuleId) || !seen.Add(check.RuleId) || check.RuleVersion != 1 ||
                !Enum.IsDefined(check.Status) || string.IsNullOrWhiteSpace(check.Evidence) || check.Evidence.Length > 4000 ||
                check.Evidence.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')) ||
                check.ObservedAtUtc.Offset != TimeSpan.Zero || check.ObservedAtUtc < report.StartedAtUtc.AddSeconds(-5) ||
                check.ObservedAtUtc > report.CompletedAtUtc.AddSeconds(5)) throw Invalid();
            var status = check.Status switch
            {
                CheckStatus.Passed => "passed", CheckStatus.Attention => "attention",
                CheckStatus.Unknown => "unknown", _ => "notApplicable"
            };
            checks.Add(new SubmittedCheck(check.RuleId, check.RuleVersion, status, check.Evidence, check.ObservedAtUtc));
        }
        return new AnalysisSubmission(report.ReportId, 2, report.AgentVersion,
            report.StartedAtUtc, report.CompletedAtUtc, checks.AsReadOnly());
    }

    private static AgentException Invalid() => new(
        "El informe no es compatible con el envío. Ejecutá scan para generar uno nuevo con las diez comprobaciones.");
}
