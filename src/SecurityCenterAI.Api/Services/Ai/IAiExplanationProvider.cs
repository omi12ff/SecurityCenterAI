using SecurityCenterAI.Api.Contracts;

namespace SecurityCenterAI.Api.Services.Ai;

// Closed, server-owned data only; no account/device/report identifiers or agent free text.
public sealed record AiExplanationInput(int? Score, int? CoveragePercent,
    IReadOnlyList<AiExplanationInputCheck> Checks);
public sealed record AiExplanationInputCheck(string RuleId, int RuleVersion, string Title,
    string Status, string Meaning, string WhatWasChecked, string VerificationLimit);
public sealed record AiProviderExplanation(string Summary, IReadOnlyList<AiExplanationCheckResponse> Checks);
public sealed record AiProviderResult(string Status, AiProviderExplanation? Explanation, string? FailureCode = null)
{
    public static AiProviderResult Unavailable(string status = "unavailable", string? failureCode = null) => new(status, null, failureCode);
}
public interface IAiExplanationProvider
{
    Task<AiProviderResult> ExplainAsync(AiExplanationInput input, CancellationToken cancellationToken);
}
