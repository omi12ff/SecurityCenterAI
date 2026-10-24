using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SecurityCenterAI.Api.Contracts;

public sealed record OfficialReference(string Title, string Url);
public sealed record CheckExplanationResponse(Guid FindingId, string Meaning, string WhatWasChecked,
    string WhyItMatters, string SuggestedAction, string VerificationLimit, IReadOnlyList<OfficialReference> References);
public sealed record AnalysisExplanationResponse(DeviceAnalysisResponse Analysis, string DeviceName,
    string Method, int ExplanationVersion, string Summary, IReadOnlyList<string> NextSteps,
    IReadOnlyList<CheckExplanationResponse> Checks, AnalysisFeedbackResponse? Feedback);
public sealed record AnalysisFeedbackResponse(string Rating, string? Comment, int ExplanationVersion, DateTime UpdatedAtUtc);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnalysisFeedbackRequest([Required] string Rating, [MaxLength(1000)] string? Comment);
