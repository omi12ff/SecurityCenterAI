namespace SecurityCenterAI.Domain.Entities;

public sealed class SecurityFinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SecurityAnalysisId { get; set; }
    public SecurityAnalysis Analysis { get; set; } = null!;
    public required string RuleId { get; set; }
    public int RuleVersion { get; set; }
    public SecurityRule Rule { get; set; } = null!;
    public required string Title { get; set; }
    public required string Severity { get; set; }
    public required string Evidence { get; set; }
    public required string Recommendation { get; set; }
    public DateTime DetectedAtUtc { get; set; }
    public string? Status { get; set; }
}
