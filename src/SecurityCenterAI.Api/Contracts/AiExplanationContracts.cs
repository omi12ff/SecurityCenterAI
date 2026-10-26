using System.Text.Json.Serialization;

namespace SecurityCenterAI.Api.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AiExplanationRequest(bool Confirmed);
public sealed record AiExplanationAvailabilityResponse(bool Available, string Code, string Message,
    string? Provider, string? Model, int DailyLimit, string DataDisclosure);
public sealed record AiExplanationCheckResponse(string RuleId, string Explanation);
public sealed record AiAnalysisExplanationResponse(string Method, string Status, int ExplanationVersion,
    string Summary, IReadOnlyList<AiExplanationCheckResponse> Checks, string? Provider, string? Model,
    DateTime GeneratedAtUtc, string Notice, string? FailureCode = null);
