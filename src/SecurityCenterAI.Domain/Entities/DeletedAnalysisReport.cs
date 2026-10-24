namespace SecurityCenterAI.Domain.Entities;

// Minimal receipt retained so a retry cannot recreate deliberately deleted history.
public sealed class DeletedAnalysisReport
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public Guid ReportId { get; set; }
    public DateTime DeletedAtUtc { get; set; }
}
