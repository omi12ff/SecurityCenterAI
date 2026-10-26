using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services.Ai;

public sealed class AnalysisAiExplanationService(AnalysisGuidanceService guidance, AppDbContext database,
    IOptions<AiExplanationOptions> options, IAiExplanationProvider provider, AiExplanationQuota quota, TimeProvider clock)
{
    private const string Disclosure = "Solo se envían la puntuación, la cobertura y los estados de las diez reglas, junto con sus textos locales revisados. No se envían tu identidad, el nombre del equipo, evidencia, comentarios ni identificadores del informe.";
    private const string AiNotice = "Texto generado con IA que puede contener errores. Revisalo junto con la guía local y la evidencia original; no cambia la puntuación, no ejecuta comandos y no modifica Windows.";

    public async Task<AiExplanationAvailabilityResponse> AvailabilityAsync(Guid userId, Guid deviceId, Guid analysisId,
        CancellationToken cancellationToken)
    {
        var (guide, schemaVersion) = await ReadAsync(userId, deviceId, analysisId, cancellationToken);
        var status = Availability(AiExplanationInputFactory.Create(guide, schemaVersion));
        return new(status == "available", status, AvailabilityMessage(status),
            status == "available" ? "omniroute" : null, status == "available" ? options.Value.Model : null,
            options.Value.DailyLimit, Disclosure);
    }

    public async Task<AiAnalysisExplanationResponse> ExplainAsync(Guid userId, Guid deviceId, Guid analysisId,
        AiExplanationRequest request, CancellationToken cancellationToken)
    {
        var (guide, schemaVersion) = await ReadAsync(userId, deviceId, analysisId, cancellationToken);
        if (!request.Confirmed)
            throw new DeviceOperationException(400, "Solicitá la explicación con el botón del análisis para autorizar el envío de los datos resumidos.");

        var input = AiExplanationInputFactory.Create(guide, schemaVersion);
        var availability = Availability(input);
        if (availability != "available") return Local(guide, availability);

        using var reservation = quota.Reserve(userId, options.Value.DailyLimit);
        AiProviderResult result;
        try { result = await provider.ExplainAsync(input!, cancellationToken); }
        catch (HttpRequestException) { result = AiProviderResult.Unavailable(); }
        catch (IOException) { result = AiProviderResult.Unavailable(); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { result = AiProviderResult.Unavailable(); }

        // A report or its device can be removed while the external request is in flight.
        // Read the owned guide again before returning either generated or local content.
        (guide, _) = await ReadAsync(userId, deviceId, analysisId, cancellationToken);
        if (result.Status == "generated" && result.Explanation is { } explanation)
            return new("ai-explanation", "generated", LocalAnalysisGuides.Version, explanation.Summary,
                explanation.Checks, "omniroute", options.Value.Model, clock.GetUtcNow().UtcDateTime, AiNotice);
        return Local(guide, result.Status == "invalid-response" || result.Status == "generated" ? "invalid-response" : "unavailable", result.FailureCode);
    }

    private async Task<(AnalysisExplanationResponse Guide, int? SchemaVersion)> ReadAsync(Guid userId, Guid deviceId,
        Guid analysisId, CancellationToken cancellationToken)
    {
        // Ownership is checked before configuration or verification so foreign records are always hidden.
        var guide = await guidance.ExplainAsync(userId, deviceId, analysisId, cancellationToken);
        var report = await database.SecurityAnalyses.AsNoTracking().Where(item => item.Id == analysisId &&
                item.UserId == userId && item.DeviceId == deviceId && item.Device != null && item.Device.UserId == userId)
            .Select(item => new { item.SchemaVersion }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new DeviceOperationException(404, "El análisis no está disponible en tu cuenta.");
        if (!await database.Users.AsNoTracking().AnyAsync(item => item.Id == userId && item.EmailVerified, cancellationToken))
            throw new DeviceOperationException(403, "Verificá tu correo desde Mi cuenta antes de solicitar una explicación con IA.");
        return (guide, report.SchemaVersion);
    }

    private string Availability(AiExplanationInput? input)
    {
        if (!options.Value.Enabled) return "disabled";
        if (!options.Value.IsConfigured()) return "unconfigured";
        return input is null ? "unsupported" : "available";
    }

    private AiAnalysisExplanationResponse Local(AnalysisExplanationResponse guide, string status, string? failureCode = null) =>
        new("local-guide", status, guide.ExplanationVersion, guide.Summary,
            guide.Analysis.Findings.Select(finding =>
            {
                var local = guide.Checks.Single(item => item.FindingId == finding.Id);
                return new AiExplanationCheckResponse(finding.RuleId,
                    $"{local.Meaning} {local.WhatWasChecked} {local.VerificationLimit}");
            }).ToArray(), null, null, clock.GetUtcNow().UtcDateTime,
            "La IA no generó una explicación. La guía local sigue disponible y conserva la puntuación y la evidencia originales.", failureCode);

    private static string AvailabilityMessage(string status) => status switch
    {
        "available" => "Usá el botón para solicitar una explicación con IA de este análisis.",
        "disabled" => "Las explicaciones con IA están desactivadas. Podés consultar la guía local.",
        "unconfigured" => "Las explicaciones con IA aún no están configuradas. Podés consultar la guía local.",
        _ => "Este informe no es compatible con la explicación con IA. Podés consultar su guía local."
    };
}
