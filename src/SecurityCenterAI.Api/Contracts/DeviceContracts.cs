using System.ComponentModel.DataAnnotations;

namespace SecurityCenterAI.Api.Contracts;

public sealed record DeviceResponse(Guid Id, string Name, DateTime CreatedAtUtc, DateTime? PairedAtUtc,
    DateTime? LastSeenAtUtc, DateTime? RevokedAtUtc, string? Platform, string? AgentVersion,
    string Status, DateTime? CredentialExpiresAtUtc);
public sealed record DevicePairingResponse(DeviceResponse Device, string PairingCode, DateTime ExpiresAtUtc);
public sealed record DevicePairResponse(Guid DeviceId, string DeviceToken, DateTime ExpiresAtUtc);
public sealed record DeviceDetailResponse(DeviceResponse Device, IReadOnlyList<DeviceAnalysisResponse> Analyses);
public sealed record DeviceAnalysisResponse(Guid Id, int? Score, DateTime AnalyzedAtUtc, IReadOnlyList<SecurityFindingResponse> Findings,
    string? Source, int? ScoringVersion, int? CoveragePercent, int? PassedCount, int? AttentionCount,
    int? UnknownCount, int? NotApplicableCount, string? AgentVersion, DateTime? StartedAtUtc, DateTime? CompletedAtUtc, Guid? ReportId);
public sealed record SecurityFindingResponse(Guid Id, string RuleId, int RuleVersion, string Title, string Severity,
    string Evidence, string Recommendation, DateTime DetectedAtUtc, string? Status);
public sealed record CreateDeviceRequest([Required, StringLength(100, MinimumLength = 2)] string Name);
public sealed record PairDeviceRequest([Required, MaxLength(256)] string PairingCode, [Required, MaxLength(64)] string AgentVersion);
public sealed record DeviceHeartbeatRequest([Required, MaxLength(64)] string AgentVersion);
