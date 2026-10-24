namespace SecurityCenterAI.Domain.Entities;

public sealed class EmailChangeRequest
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string CurrentEmail { get; set; }
    public required string NewEmail { get; set; }
    public required string CurrentTokenHash { get; set; }
    public required string NewTokenHash { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? CurrentConfirmedAtUtc { get; set; }
    public DateTime? NewConfirmedAtUtc { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
