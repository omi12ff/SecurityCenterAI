using Microsoft.EntityFrameworkCore;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Domain.Rules;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services;

public sealed class AnalysisComparisonService(AppDbContext database)
{
    private static readonly IReadOnlyDictionary<string, WindowsRule> Rules =
        WindowsRuleCatalog.Rules.ToDictionary(rule => rule.Id, StringComparer.Ordinal);

    public async Task<AnalysisComparisonResponse> CompareAsync(Guid userId, Guid deviceId,
        Guid baselineId, Guid targetId, CancellationToken cancellationToken)
    {
        if (baselineId == Guid.Empty || targetId == Guid.Empty)
            throw new DeviceOperationException(400, "Elegí dos análisis válidos para comparar.");

        var device = await database.Devices.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == deviceId && item.UserId == userId, cancellationToken)
            ?? throw Missing();
        var analyses = await database.SecurityAnalyses.AsNoTracking().Include(item => item.Findings
                .OrderBy(finding => finding.RuleId).ThenBy(finding => finding.Id).Take(100))
            .Where(item => item.UserId == userId && item.DeviceId == deviceId &&
                (item.Id == baselineId || item.Id == targetId)).ToListAsync(cancellationToken);
        var baseline = analyses.SingleOrDefault(item => item.Id == baselineId) ?? throw Missing();
        var target = analyses.SingleOrDefault(item => item.Id == targetId) ?? throw Missing();
        if (baselineId == targetId || baseline.AnalyzedAtUtc > target.AnalyzedAtUtc)
            throw new DeviceOperationException(400, "Elegí dos análisis distintos y usá el anterior como base.");
        if (baseline.CompletedAtUtc.HasValue && target.CompletedAtUtc.HasValue && baseline.CompletedAtUtc > target.CompletedAtUtc)
            throw new DeviceOperationException(400, "Las fechas de las comprobaciones están en orden inverso. Elegí una base con comprobaciones anteriores al segundo análisis.");

        var supported = SupportsComparison(baseline) && SupportsComparison(target);
        var before = baseline.Findings.GroupBy(item => item.RuleId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var after = target.Findings.GroupBy(item => item.RuleId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var checks = before.Keys.Union(after.Keys, StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => CompareCheck(id, before.GetValueOrDefault(id), after.GetValueOrDefault(id), supported)).ToArray();

        // Compare the stored points only when the scoring rules and applicable rule set match.
        // A newly observable result may add points without proving a configuration change.
        var scoreComparable = supported && CompleteCatalog(before) && CompleteCatalog(after) &&
            baseline.Score.HasValue && target.Score.HasValue &&
            baseline.CoveragePercent.HasValue && target.CoveragePercent.HasValue &&
            before.Keys.All(id => (before[id][0].Status == "notApplicable") == (after[id][0].Status == "notApplicable"));
        var summary = new AnalysisComparisonSummary(checks.Count(item => item.Change == "improved"),
            checks.Count(item => item.Change == "needsReview"), checks.Count(item => item.Change == "newInformation"),
            checks.Count(item => item.Change == "lostInformation"), checks.Count(item => item.Change == "unchanged"),
            checks.Count(item => item.Change == "notComparable"));
        return new(device.Id, device.Name, DeviceService.ToAnalysisResponse(baseline), DeviceService.ToAnalysisResponse(target),
            scoreComparable, scoreComparable ? target.Score - baseline.Score : null,
            scoreComparable ? target.CoveragePercent - baseline.CoveragePercent : null, summary, checks);
    }

    private static bool SupportsComparison(SecurityAnalysis analysis) =>
        analysis.Source == "windows-agent" && analysis.SchemaVersion == 2 && analysis.ScoringVersion == WindowsRuleCatalog.Version;

    private static bool SupportedStatus(string? status) => status is "passed" or "attention" or "unknown" or "notApplicable";

    private static bool CompleteCatalog(IReadOnlyDictionary<string, SecurityFinding[]> findings) =>
        findings.Count == Rules.Count && Rules.Keys.All(id => findings.TryGetValue(id, out var matches) &&
            matches.Length == 1 && matches[0].RuleVersion == WindowsRuleCatalog.Version && SupportedStatus(matches[0].Status));

    private static AnalysisCheckComparison CompareCheck(string id, SecurityFinding[]? before,
        SecurityFinding[]? after, bool supported)
    {
        var baseline = before is { Length: 1 } ? before[0] : null;
        var target = after is { Length: 1 } ? after[0] : null;
        var title = Rules.TryGetValue(id, out var rule) ? rule.Title : target?.Title ?? baseline?.Title ?? id;
        var version = baseline is not null && target is not null
            ? baseline.RuleVersion == target.RuleVersion ? baseline.RuleVersion : (int?)null
            : baseline?.RuleVersion ?? target?.RuleVersion;
        var change = "notComparable";
        string explanation;
        if (!supported)
            explanation = "Los análisis no comparten una fuente y una versión admitidas. Consultá la evidencia original sin inferir cambios.";
        else if (baseline is null || target is null)
            explanation = "Esta regla no figura una sola vez en ambos análisis. La ausencia o duplicación no demuestra una mejora ni un problema nuevo.";
        else if (rule is null || baseline.RuleVersion != WindowsRuleCatalog.Version || target.RuleVersion != WindowsRuleCatalog.Version ||
            !SupportedStatus(baseline.Status) || !SupportedStatus(target.Status))
            explanation = "La regla, su versión o sus estados no tienen una interpretación común admitida. Consultá ambos registros originales.";
        else if (baseline.Status == target.Status)
        {
            change = "unchanged";
            explanation = "El estado informado coincide; la evidencia y el momento pueden ser distintos. " + (target.Status switch
            {
                "unknown" => "El estado sigue siendo desconocido. No confirma una protección activa ni un fallo y no representa una mejora.",
                "notApplicable" => "Ambas observaciones informaron que la regla no era aplicable; no confirma el estado de una protección.",
                "attention" => "Ambas observaciones requieren revisión según esta regla. Contrastá la evidencia con el estado actual del equipo.",
                _ => "Ambas observaciones cumplían el criterio de esta regla en sus respectivos momentos; no garantizan el estado actual del equipo."
            });
        }
        else if (baseline.Status == "notApplicable" || target.Status == "notApplicable")
            explanation = "Cambió la aplicabilidad de esta regla. Los estados no permiten inferir una mejora o un deterioro equivalente.";
        else if (target.Status == "unknown")
        {
            change = "lostInformation";
            explanation = "El análisis más reciente no pudo determinar un estado antes conocido. Es una pérdida de información, sin confirmar un fallo nuevo.";
        }
        else if (baseline.Status == "unknown")
        {
            change = "newInformation";
            explanation = "Ahora se dispone de un estado antes desconocido. Esta nueva información no demuestra por sí sola un cambio de configuración.";
        }
        else if (target.Status == "passed")
        {
            change = "improved";
            explanation = "La observación pasó de requerir revisión a cumplir el criterio. No demuestra qué acción produjo el cambio ni una corrección realizada por el agente.";
        }
        else
        {
            change = "needsReview";
            explanation = "La observación pasó de cumplir el criterio a requerir revisión. Contrastá ambas evidencias; este cambio no demuestra una infección.";
        }
        return new(id, version, title, baseline?.Id, target?.Id, baseline?.Status, target?.Status, change, explanation);
    }

    private static DeviceOperationException Missing() => new(404, "Los análisis no están disponibles para este dispositivo en tu cuenta.");
}
