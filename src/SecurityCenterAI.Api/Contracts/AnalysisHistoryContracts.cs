using System.Text.Json.Serialization;

namespace SecurityCenterAI.Api.Contracts;

public sealed record ExportedAnalysisDevice(Guid Id, string Name);
public sealed record AnalysisExportResponse(int ExportVersion, DateTime ExportedAtUtc,
    ExportedAnalysisDevice Device, DeviceAnalysisResponse Analysis, AnalysisFeedbackResponse? Feedback);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeleteAnalysisRequest(bool Confirmed);
