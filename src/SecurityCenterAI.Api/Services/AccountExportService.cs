using System.Data;
using Microsoft.EntityFrameworkCore;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services;

public sealed class AccountExportService(AppDbContext database, TimeProvider clock)
{
    public async Task<AccountExportResponse> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Serial reads under a single PostgreSQL snapshot keep reports, feedback and
        // deletion receipts consistent if history changes while exporting.
        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
        var exportedAt = clock.GetUtcNow().UtcDateTime;
        var profile = await database.Users.AsNoTracking().Where(item => item.Id == userId)
            .Select(item => new AccountExportProfile(item.Id, item.Name, item.Email, item.CreatedAtUtc, item.EmailVerified))
            .SingleOrDefaultAsync(cancellationToken) ?? throw new DeviceOperationException(404, "La cuenta no está disponible.");
        var devices = await database.Devices.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        // Only expiry metadata enters memory; credentials and their hashes are excluded.
        var expirations = await database.DeviceCredentials.AsNoTracking().Where(item => item.Device.UserId == userId &&
                item.RevokedAtUtc == null && item.ExpiresAtUtc > exportedAt)
            .Select(item => new { item.DeviceId, item.ExpiresAtUtc }).ToListAsync(cancellationToken);
        var expiryByDevice = expirations.GroupBy(item => item.DeviceId)
            .ToDictionary(group => group.Key, group => group.Max(item => item.ExpiresAtUtc));
        // Export all stored history, including pre-device records, without UI limits.
        var analyses = await database.SecurityAnalyses.AsNoTracking().Include(item => item.Findings)
            .Where(item => item.UserId == userId && (item.DeviceId == null || item.Device != null && item.Device.UserId == userId))
            .OrderBy(item => item.AnalyzedAtUtc).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        var feedback = await database.AnalysisFeedback.AsNoTracking().Where(item => item.UserId == userId)
            .Select(item => new { item.AnalysisId, item.Rating, item.Comment, item.ExplanationVersion, item.UpdatedAtUtc })
            .ToDictionaryAsync(item => item.AnalysisId, item => new AnalysisFeedbackResponse(item.Rating, item.Comment,
                item.ExplanationVersion, item.UpdatedAtUtc), cancellationToken);
        var deletedReports = await database.DeletedAnalysisReports.AsNoTracking().Where(item => item.Device.UserId == userId)
            .OrderBy(item => item.DeletedAtUtc).ThenBy(item => item.DeviceId).ThenBy(item => item.ReportId)
            .Select(item => new AccountExportDeletedReport(item.DeviceId, item.ReportId, item.DeletedAtUtc))
            .ToListAsync(cancellationToken);
        var activity = await database.SecurityActivities.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.OccurredAtUtc).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        var pendingEmail = await database.EmailChangeRequests.AsNoTracking().Where(item => item.UserId == userId)
            .Select(item => new AccountExportEmailChange(item.NewEmail, item.CreatedAtUtc, item.ExpiresAtUtc,
                item.CurrentConfirmedAtUtc.HasValue, item.NewConfirmedAtUtc.HasValue)).SingleOrDefaultAsync(cancellationToken);
        var toolUsage = await database.LinkMessageUsage.AsNoTracking().Where(item => item.UserId == userId)
            .Select(item => new AccountExportLinkMessageUsage(item.DayUtc, item.Checks)).SingleOrDefaultAsync(cancellationToken);
        var response = new AccountExportResponse(1, exportedAt, profile,
            devices.Select(device => DeviceService.ToDeviceResponse(device,
                expiryByDevice.TryGetValue(device.Id, out var expiry) ? expiry : null)).ToArray(),
            analyses.Select(analysis => new AccountExportAnalysis(analysis.DeviceId, DeviceService.ToAnalysisResponse(analysis),
                feedback.GetValueOrDefault(analysis.Id))).ToArray(), deletedReports,
            activity.Select(SecurityActivityRecorder.ToResponse).ToArray(), pendingEmail, toolUsage);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return response;
    }
}
