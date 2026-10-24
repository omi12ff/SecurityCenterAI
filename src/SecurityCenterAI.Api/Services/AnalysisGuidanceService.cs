using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services;

public sealed class AnalysisGuidanceService(AppDbContext database, TimeProvider clock)
{
    private static DeviceOperationException Missing() => new(404, "El análisis no está disponible en tu cuenta.");

    private async Task<SecurityAnalysis> OwnedAsync(Guid userId, Guid deviceId, Guid analysisId, CancellationToken cancellationToken) =>
        await database.SecurityAnalyses.AsNoTracking().Include(item => item.Device).Include(item => item.Findings
                .OrderBy(finding => finding.RuleId).ThenBy(finding => finding.Id).Take(100))
            .SingleOrDefaultAsync(item => item.Id == analysisId && item.UserId == userId && item.DeviceId == deviceId &&
                item.Device != null && item.Device.UserId == userId, cancellationToken) ?? throw Missing();

    public async Task<AnalysisExplanationResponse> ExplainAsync(Guid userId, Guid deviceId, Guid analysisId, CancellationToken cancellationToken)
    {
        var analysis = await OwnedAsync(userId, deviceId, analysisId, cancellationToken);
        var feedback = await database.AnalysisFeedback.AsNoTracking().SingleOrDefaultAsync(item =>
            item.UserId == userId && item.AnalysisId == analysisId, cancellationToken);
        var steps = new List<string>();
        if (analysis.Findings.Any(item => item.Status == "attention")) steps.Add("Empezá por las comprobaciones para revisar y contrastá su evidencia con el estado actual de Windows.");
        if (analysis.Findings.Any(item => item.Status == "unknown")) steps.Add("Consultá manualmente los estados desconocidos antes de decidir si hace falta un cambio.");
        if (steps.Count == 0) steps.Add("Consultá los límites de cada comprobación y volvé a analizar cuando cambie la configuración del equipo.");
        steps.Add("Después de revisar el equipo, podés enviar un nuevo análisis desde el agente; este registro conserva su resultado original.");
        return new(DeviceService.ToAnalysisResponse(analysis), analysis.Device!.Name, "local-guide", LocalAnalysisGuides.Version,
            "Guía local basada en reglas y en el informe recibido. Los puntos confirmados y la cobertura conservan su valor original; los estados desconocidos no confirman problemas.",
            steps, analysis.Findings.Select(finding => LocalAnalysisGuides.Explain(finding, analysis.Source)).ToArray(),
            feedback is null ? null : Response(feedback));
    }

    public async Task<AnalysisFeedbackResponse> SaveFeedbackAsync(Guid userId, Guid deviceId, Guid analysisId,
        AnalysisFeedbackRequest request, CancellationToken cancellationToken)
    {
        var comment = request.Comment?.Trim();
        if (request.Rating is not ("helpful" or "unclear" or "incorrect") || comment?.Length > 1000 ||
            comment?.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')) == true)
            throw new DeviceOperationException(400, "Elegí una valoración válida y un comentario de hasta 1000 caracteres.");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await OwnedAsync(userId, deviceId, analysisId, cancellationToken);
            var feedback = await database.AnalysisFeedback.SingleOrDefaultAsync(item => item.UserId == userId && item.AnalysisId == analysisId, cancellationToken);
            var now = clock.GetUtcNow().UtcDateTime;
            if (feedback is null)
            {
                feedback = new AnalysisFeedback { UserId = userId, AnalysisId = analysisId, Rating = request.Rating, CreatedAtUtc = now };
                database.AnalysisFeedback.Add(feedback);
            }
            feedback.Rating = request.Rating;
            feedback.Comment = string.IsNullOrEmpty(comment) ? null : comment;
            feedback.ExplanationVersion = LocalAnalysisGuides.Version;
            feedback.UpdatedAtUtc = now;
            try { await database.SaveChangesAsync(cancellationToken); return Response(feedback); }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_analysis_feedback_id_usuario_id_analisis" })
            { database.ChangeTracker.Clear(); }
            catch (DbUpdateConcurrencyException)
            {
                database.ChangeTracker.Clear();
                await OwnedAsync(userId, deviceId, analysisId, cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName: "FK_analysis_feedback_security_analyses_id_analisis" })
            {
                database.ChangeTracker.Clear();
                throw Missing();
            }
        }
        throw new DeviceOperationException(409, "Otro envío actualizó tu valoración. Volvé a intentar.");
    }

    private static AnalysisFeedbackResponse Response(AnalysisFeedback feedback) =>
        new(feedback.Rating, feedback.Comment, feedback.ExplanationVersion, feedback.UpdatedAtUtc);
}
