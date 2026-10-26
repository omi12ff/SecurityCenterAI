using System.Text.Json;
using SecurityCenterAI.Api.Contracts;

namespace SecurityCenterAI.Api.Services.Ai;

public static class AiExplanationOutput
{
    public static AiProviderExplanation? Parse(string json, AiExplanationInput input) => Parse(json, input, out _);

    public static AiProviderExplanation? Parse(string json, AiExplanationInput input, out string? rejectionReason)
    {
        rejectionReason = "output-size";
        if (json.Length > 24000) return null;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            rejectionReason = "summary-shape-or-content";
            if (!Shape(root, "summary", "checks") || !Text(root.GetProperty("summary"), 2000, out var summary)) return null;
            var checks = root.GetProperty("checks");
            rejectionReason = "rule-count";
            if (checks.ValueKind != JsonValueKind.Array || checks.GetArrayLength() != input.Checks.Count) return null;
            var output = new Dictionary<string, AiExplanationCheckResponse>(StringComparer.Ordinal);
            foreach (var check in checks.EnumerateArray())
            {
                rejectionReason = "rule-shape-or-content";
                if (!Shape(check, "ruleId", "explanation") || check.GetProperty("ruleId").ValueKind != JsonValueKind.String ||
                    !Text(check.GetProperty("explanation"), 1600, out var explanation)) return null;
                var id = check.GetProperty("ruleId").GetString()!;
                var original = input.Checks.SingleOrDefault(item => item.RuleId == id);
                rejectionReason = "rule-identity";
                if (original is null || !output.TryAdd(id, new(id, explanation))) return null;
                // Unknown observations must explicitly preserve uncertainty in the generated text.
                rejectionReason = "unknown-uncertainty";
                if (original.Status == "unknown" && !new[] { "desconocid", "no se pudo", "no se puede", "no fue posible", "no confirma",
                    "no permite", "sin datos", "falta información", "insuficiente", "no determinado", "no disponible", "no sabemos" }
                    .Any(part => explanation.Contains(part, StringComparison.OrdinalIgnoreCase))) return null;
            }
            rejectionReason = null;
            return new(summary, input.Checks.Select(check => output[check.RuleId]).ToArray());
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        { rejectionReason = "invalid-output-json"; return null; }
    }
    private static bool Shape(JsonElement element, params string[] expected) => element.ValueKind == JsonValueKind.Object &&
        element.EnumerateObject().Count() == expected.Length && expected.All(name => element.TryGetProperty(name, out _));
    private static bool Text(JsonElement element, int maximum, out string text)
    {
        text = element.ValueKind == JsonValueKind.String ? element.GetString()?.Trim() ?? "" : "";
        var value = text;
        return text.Length is > 0 && text.Length <= maximum && !text.Any(c => char.IsControl(c) && c is not ('\r' or '\n')) &&
            !text.Any(c => c is '<' or '>' or '`') && !new[] { "http://", "https://", "powershell", "cmd.exe", "invoke-expression", "remove-item",
                "set-mppreference", "reg add", "$env:", "sfc /", "dism /", "netsh ", "set-service", "disable-windowsoptionalfeature",
                "enable-windowsoptionalfeature", "set-itemproperty", "100% seguro", "completamente seguro", "invulnerable" }
                .Any(part => value.Contains(part, StringComparison.OrdinalIgnoreCase));
    }
}
