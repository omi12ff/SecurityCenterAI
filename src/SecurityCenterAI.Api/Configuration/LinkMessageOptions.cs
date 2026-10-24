namespace SecurityCenterAI.Api.Configuration;

public sealed class LinkMessageOptions
{
    public const string SectionName = "LinkMessageChecks";
    public int DailyLimit { get; init; } = 25;
}
