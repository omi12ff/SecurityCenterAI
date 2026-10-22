namespace SecurityCenterAI.Domain.Entities;

public sealed class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Name { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PairedAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? Platform { get; set; }
    public string? AgentVersion { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
