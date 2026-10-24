namespace SecurityCenterAI.Api.Contracts;

public sealed record AccountExportProfile(Guid Id, string Name, string Email, DateTime CreatedAtUtc, bool EmailVerified);
public sealed record AccountExportAnalysis(Guid? DeviceId, DeviceAnalysisResponse Analysis, AnalysisFeedbackResponse? Feedback);
public sealed record AccountExportDeletedReport(Guid DeviceId, Guid ReportId, DateTime DeletedAtUtc);
public sealed record AccountExportEmailChange(string NewEmail, DateTime CreatedAtUtc, DateTime ExpiresAtUtc,
    bool CurrentAddressConfirmed, bool NewAddressConfirmed);
public sealed record AccountExportLinkMessageUsage(DateOnly DayUtc, int Checks);
public sealed record AccountExportResponse(int ExportVersion, DateTime ExportedAtUtc, AccountExportProfile Profile,
    IReadOnlyList<DeviceResponse> Devices, IReadOnlyList<AccountExportAnalysis> Analyses,
    IReadOnlyList<AccountExportDeletedReport> DeletedReports,
    IReadOnlyList<SecurityActivityResponse>? SecurityActivity = null, AccountExportEmailChange? PendingEmailChange = null,
    AccountExportLinkMessageUsage? LinkMessageUsage = null);
