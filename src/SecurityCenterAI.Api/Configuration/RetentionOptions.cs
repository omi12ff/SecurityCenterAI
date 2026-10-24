namespace SecurityCenterAI.Api.Configuration;

public sealed class RetentionOptions
{
    public const string SectionName = "Retention";
    public bool Enabled { get; init; }
    public int ClosedSessionDays { get; init; } = 30;
    public int AccountTokenDays { get; init; } = 7;
    public int PairingCodeDays { get; init; } = 7;
    public int DeviceCredentialDays { get; init; } = 30;
    public int BatchSize { get; init; } = 200;
    public int IntervalHours { get; init; } = 24;

    public static bool Valid(RetentionOptions value) => value.ClosedSessionDays is >= 7 and <= 365 &&
        value.AccountTokenDays is >= 1 and <= 365 && value.PairingCodeDays is >= 1 and <= 365 &&
        value.DeviceCredentialDays is >= 7 and <= 365 && value.BatchSize is >= 1 and <= 1000 &&
        value.IntervalHours is >= 1 and <= 168;
}
