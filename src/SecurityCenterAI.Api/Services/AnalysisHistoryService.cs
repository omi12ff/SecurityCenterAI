using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services;

public sealed class AnalysisHistoryService(AppDbContext database, TimeProvider clock)
{
    private static DeviceOperationException Missing() => new(404, "El análisis no está disponible en tu cuenta.");

    public async Task<AnalysisExportResponse> ExportAsync(Guid userId, Guid deviceId, Guid analysisId,
        CancellationToken cancellationToken)
    {
        // Export the full stored report, independently of the history page's display limits.
        var analysis = await database.SecurityAnalyses.AsNoTracking().Include(item => item.Device)
            .Include(item => item.Findings).SingleOrDefaultAsync(item => item.Id == analysisId &&
                item.UserId == userId && item.DeviceId == deviceId && item.Device != null &&
                item.Device.UserId == userId, cancellationToken) ?? throw Missing();
        var feedback = await database.AnalysisFeedback.AsNoTracking().SingleOrDefaultAsync(item =>
            item.AnalysisId == analysisId && item.UserId == userId, cancellationToken);
        // Explicit DTOs exclude hashes, account data and authentication material.
        return new(1, clock.GetUtcNow().UtcDateTime, new(analysis.Device!.Id, analysis.Device.Name),
            DeviceService.ToAnalysisResponse(analysis), feedback is null ? null :
                new(feedback.Rating, feedback.Comment, feedback.ExplanationVersion, feedback.UpdatedAtUtc));
    }

    public async Task DeleteAsync(Guid userId, Guid deviceId, Guid analysisId, DeleteAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.Confirmed)
            throw new DeviceOperationException(400, "Confirmá expresamente que querés eliminar este análisis.");
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await using var transaction = database.Database.IsRelational()
                ? await database.Database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
                var analysis = await database.SecurityAnalyses.Include(item => item.Device).ThenInclude(item => item!.User)
                    .Include(item => item.Findings).SingleOrDefaultAsync(item => item.Id == analysisId &&
                        item.UserId == userId && item.DeviceId == deviceId && item.Device != null &&
                        item.Device.UserId == userId, cancellationToken) ?? throw Missing();
                var feedback = await database.AnalysisFeedback.Where(item => item.AnalysisId == analysisId)
                    .ToListAsync(cancellationToken);
                var device = analysis.Device!;
                if (analysis.ReportId is { } reportId && !await database.DeletedAnalysisReports.AnyAsync(item =>
                    item.DeviceId == deviceId && item.ReportId == reportId, cancellationToken))
                    database.DeletedAnalysisReports.Add(new DeletedAnalysisReport
                    { DeviceId = deviceId, ReportId = reportId, DeletedAtUtc = clock.GetUtcNow().UtcDateTime });

                // Ingestion writes these same optimistic guards. A concurrent upload
                // must retry and observe the minimal deletion receipt before acceptance.
                device.ConcurrencyStamp = Guid.NewGuid();
                device.User.ConcurrencyStamp = Guid.NewGuid();
                database.AnalysisFeedback.RemoveRange(feedback);
                database.SecurityAnalyses.Remove(analysis);
                SecurityActivityRecorder.Add(database, userId, SecurityActivityKind.AnalysisDeleted, clock.GetUtcNow().UtcDateTime);
                await database.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                database.ChangeTracker.Clear();
            }
        }
        throw new DeviceOperationException(409, "Otra operación modificó el historial. Volvé a intentar eliminar el análisis.");
    }

    private static bool IsRetryable(Exception exception) => exception is DbUpdateConcurrencyException ||
        exception is InvalidOperationException { InnerException: { } inner } && IsRetryable(inner) ||
        IsRetryablePostgres(exception as PostgresException) ||
        exception is DbUpdateException { InnerException: PostgresException postgres } && IsRetryablePostgres(postgres);

    private static bool IsRetryablePostgres(PostgresException? exception) => exception is not null &&
        (exception.SqlState is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure ||
         exception is { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_deleted_analysis_reports" });
}
