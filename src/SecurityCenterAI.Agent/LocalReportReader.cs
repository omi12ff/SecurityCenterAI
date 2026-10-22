using System.Text.Json;
using System.Text.Json.Serialization;
using SecurityCenterAI.Agent.Checks;
using SecurityCenterAI.Agent.Transport;

namespace SecurityCenterAI.Agent;

public static class LocalReportReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public static async Task<AgentReport> ReadLatestAsync(string storageDirectory, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetFullPath(storageDirectory), "reports");
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw Invalid();
        if (!Directory.Exists(directory)) throw Missing();
        var file = new DirectoryInfo(directory).EnumerateFiles("scan-*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(item => item.LastWriteTimeUtc).ThenByDescending(item => item.Name).FirstOrDefault()
            ?? throw Missing();
        if (file.Attributes.HasFlag(FileAttributes.ReparsePoint) || file.Length is <= 0 or > 65536) throw Invalid();
        try
        {
            await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > 65536) throw Invalid();
            var bytes = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(bytes, cancellationToken);
            using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
            if (!json.RootElement.TryGetProperty("reportId", out var id) || !id.TryGetGuid(out var value) || value == Guid.Empty)
                throw Invalid();
            var report = JsonSerializer.Deserialize<AgentReport>(bytes, Options) ?? throw Invalid();
            _ = ReportSubmission.Create(report);
            return report;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
        { throw Invalid(); }
    }

    private static AgentException Missing() => new("No hay un informe local. Ejecutá scan para generar uno antes de enviar.");
    private static AgentException Invalid() => new("El último informe no es compatible o no se puede leer de forma segura. Ejecutá scan para generar uno nuevo.");
}
