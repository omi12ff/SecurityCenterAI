using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Rules;

namespace SecurityCenterAI.Api.Services.Ai;

/// <summary>Allowlist projection; stored titles, evidence and identifiers never enter the provider input.</summary>
public static class AiExplanationInputFactory
{
    public static AiExplanationInput? Create(AnalysisExplanationResponse guide, int? schemaVersion)
    {
        var analysis = guide.Analysis;
        if (schemaVersion != 2 || analysis.Source != "windows-agent" || analysis.ScoringVersion != WindowsRuleCatalog.Version ||
            analysis.Findings.Count != WindowsRuleCatalog.Rules.Count ||
            analysis.Findings.Select(item => item.RuleId).Distinct(StringComparer.Ordinal).Count() != WindowsRuleCatalog.Rules.Count ||
            analysis.Findings.Any(item => item.RuleVersion != WindowsRuleCatalog.Version ||
                item.Status is not ("passed" or "attention" or "unknown" or "notApplicable"))) return null;

        var checks = new List<AiExplanationInputCheck>();
        foreach (var rule in WindowsRuleCatalog.Rules)
        {
            var finding = analysis.Findings.SingleOrDefault(item => item.RuleId == rule.Id);
            if (finding is null) return null;
            var local = guide.Checks.Single(item => item.FindingId == finding.Id);
            checks.Add(new(rule.Id, WindowsRuleCatalog.Version, rule.Title, finding.Status!,
                local.Meaning, local.WhatWasChecked, local.VerificationLimit));
        }
        return new(analysis.Score, analysis.CoveragePercent, checks);
    }
}
