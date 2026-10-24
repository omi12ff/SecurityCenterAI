namespace SecurityCenterAI.Api.Contracts;

public sealed record SecurityActivityResponse(Guid Id, string Action, DateTime OccurredAtUtc);
public sealed record SecurityActivityListResponse(IReadOnlyList<SecurityActivityResponse> Events, bool HasMore, int Limit, Guid? NextBefore = null);

public sealed record SecurityActivityQuery
{
    public string? Action { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public string? Before { get; init; }
}
