namespace SecurityCenterAI.Api.Contracts;

public sealed record AnalysisSummaryResponse(
    Guid Id,
    int Score,
    DateTime AnalyzedAtUtc);

public sealed record DashboardResponse(
    int? SecurityScore,
    int TotalAnalyses,
    IReadOnlyList<AnalysisSummaryResponse> RecentAnalyses);
