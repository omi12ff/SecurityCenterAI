using System.Text.Json.Serialization;

namespace SecurityCenterAI.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitAnalysisRequest(Guid ReportId, int SchemaVersion, string AgentVersion,
    DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc, IReadOnlyList<SubmittedCheck> Checks);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmittedCheck(string RuleId, int RuleVersion, string Status, string Evidence, DateTimeOffset ObservedAtUtc);

public sealed record AnalysisReceipt(Guid AnalysisId, Guid ReportId, int? Score, int CoveragePercent,
    int PassedCount, int AttentionCount, int UnknownCount, int NotApplicableCount,
    int ScoringVersion, DateTime ReceivedAtUtc, bool AlreadyReceived);
