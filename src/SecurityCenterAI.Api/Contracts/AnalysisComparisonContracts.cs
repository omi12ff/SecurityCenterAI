namespace SecurityCenterAI.Api.Contracts;

public sealed record AnalysisComparisonResponse(Guid DeviceId, string DeviceName,
    DeviceAnalysisResponse Baseline, DeviceAnalysisResponse Target, bool ScoreComparable,
    int? ScoreDelta, int? CoverageDelta, AnalysisComparisonSummary Summary,
    IReadOnlyList<AnalysisCheckComparison> Checks);

public sealed record AnalysisComparisonSummary(int Improved, int NeedsReview, int NewInformation,
    int LostInformation, int Unchanged, int NotComparable);

public sealed record AnalysisCheckComparison(string RuleId, int? RuleVersion, string Title,
    Guid? BaselineFindingId, Guid? TargetFindingId, string? BaselineStatus, string? TargetStatus,
    string Change, string Explanation);
