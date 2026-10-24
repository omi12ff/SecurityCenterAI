namespace SecurityCenterAI.Api.Contracts;

public sealed record LinkMessageRequest(string Kind, string Content, bool Confirmed);
public sealed record LinkMessageQuota(DateOnly DayUtc, int Used, int Limit, int Remaining, DateTime ResetsAtUtc);
public sealed record LinkMessageSignal(string Code, string Title, string Explanation, string Advice);
public sealed record LocalScamReport(string RulesVersion, string Kind, string Outcome,
    IReadOnlyList<LinkMessageSignal> Signals, IReadOnlyList<string> Hosts, int LinksChecked, int LinksOmitted);
public sealed record LinkMessageResponse(LocalScamReport Report, LinkMessageQuota Quota, DateTime CheckedAtUtc);
