namespace SecurityCenterAI.Domain.Entities;

public sealed class AnalysisFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid AnalysisId { get; set; }
    public SecurityAnalysis Analysis { get; set; } = null!;
    public required string Rating { get; set; }
    public string? Comment { get; set; }
    public int ExplanationVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
