namespace SecurityCenterAI.Domain.Entities;

// One rolling counter per owner. No URL, message or report is persisted.
public sealed class LinkMessageUsage
{
    public Guid UserId { get; init; }
    public User User { get; init; } = null!;
    public DateOnly DayUtc { get; set; }
    public int Checks { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
