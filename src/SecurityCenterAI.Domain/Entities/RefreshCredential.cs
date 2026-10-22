namespace SecurityCenterAI.Domain.Entities;

public sealed class RefreshCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserSessionId { get; set; }
    public UserSession Session { get; set; } = null!;
    public required string TokenHash { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
