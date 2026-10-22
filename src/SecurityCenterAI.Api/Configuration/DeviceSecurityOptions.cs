namespace SecurityCenterAI.Api.Configuration;

public sealed class DeviceSecurityOptions
{
    public const string SectionName = "DeviceSecurity";
    public int PairingMinutes { get; init; } = 10;
    public int CredentialDays { get; init; } = 90;
}
