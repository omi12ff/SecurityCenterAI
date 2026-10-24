namespace SecurityCenterAI.Domain.Entities;

public enum SecurityActivityKind
{
    AccountRegistered, SessionStarted, SessionRevoked, AllSessionsRevoked, RefreshReplayBlocked,
    EmailVerified, PasswordReset, DeviceRegistered, PairingCodeIssued, DevicePaired, DeviceRevoked, AnalysisDeleted, PasswordChanged, EmailChanged
}

// Deliberately no free text, IP, session/device/report identifier or credential.
public sealed class SecurityActivity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public User User { get; init; } = null!;
    public SecurityActivityKind Kind { get; init; }
    public DateTime OccurredAtUtc { get; init; }
}
