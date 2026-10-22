namespace SecurityCenterAI.Domain.Entities;

public sealed class SecurityRule
{
    public required string Id { get; set; }
    public int Version { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? Severity { get; set; }
    public string? Recommendation { get; set; }
    public int? Weight { get; set; }
}
