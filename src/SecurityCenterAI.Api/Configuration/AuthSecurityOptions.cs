namespace SecurityCenterAI.Api.Configuration;

public sealed class AuthSecurityOptions
{
    public const string SectionName = "AuthSecurity";
    public int RefreshDays { get; init; } = 30;
    public int PasswordResetMinutes { get; init; } = 30;
    public int VerificationHours { get; init; } = 24;
    public int MaxFailedLoginAttempts { get; init; } = 5;
    public int LockoutMinutes { get; init; } = 15;
    public int EmailCooldownSeconds { get; init; } = 60;
}
