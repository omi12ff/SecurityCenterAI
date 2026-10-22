namespace SecurityCenterAI.Agent.Checks;

public enum CheckStatus
{
    Passed,
    Attention,
    Unknown,
    NotApplicable
}

public sealed record CheckResult(
    string RuleId,
    int RuleVersion,
    string Title,
    string Severity,
    CheckStatus Status,
    string Evidence,
    string Recommendation,
    DateTimeOffset ObservedAtUtc);

public sealed record AgentReport(
    int SchemaVersion,
    string AgentVersion,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<CheckResult> Checks)
{
    public Guid ReportId { get; init; } = Guid.NewGuid();
}

public enum WindowsProbeKind
{
    DefenderAntivirus,
    DefenderRealtime,
    DefenderSignatures,
    FirewallProfiles,
    SecureBoot,
    Tpm,
    BitLocker,
    WindowsUpdateService,
    Uac,
    Smb1
}

public enum ProbeOutcome
{
    Success,
    Unavailable,
    AccessDenied,
    NotApplicable,
    Timeout,
    InvalidOutput
}

public sealed record ProbeResult(ProbeOutcome Outcome, string? Json = null);

public interface IWindowsProbe
{
    Task<ProbeResult> ProbeAsync(WindowsProbeKind kind, CancellationToken cancellationToken);
}
