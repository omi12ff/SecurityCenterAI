namespace SecurityCenterAI.Domain.Entities;

public sealed class SecurityAnalysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public int? Score { get; set; }
    public DateTime AnalyzedAtUtc { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }
    public ICollection<SecurityFinding> Findings { get; set; } = [];
    public Guid? ReportId { get; set; }
    public string? ReportHash { get; set; }
    public int? SchemaVersion { get; set; }
    public string? Source { get; set; }
    public string? AgentVersion { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int? ScoringVersion { get; set; }
    public int? CoveragePercent { get; set; }
    public int? PassedCount { get; set; }
    public int? AttentionCount { get; set; }
    public int? UnknownCount { get; set; }
    public int? NotApplicableCount { get; set; }
}
