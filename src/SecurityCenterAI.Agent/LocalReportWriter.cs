using System.Text.Json;
using System.Text.Json.Serialization;
using SecurityCenterAI.Agent.Checks;

namespace SecurityCenterAI.Agent;

public static class LocalReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task<string> WriteAsync(string storageDirectory, AgentReport report, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetFullPath(storageDirectory), "reports");
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new AgentException("La carpeta de informes no debe ser un enlace o una unión de directorios.");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"scan-{report.StartedAtUtc:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.json");
        var created = false;
        try
        {
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created = true;
            await JsonSerializer.SerializeAsync(stream, report, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return path;
        }
        catch
        {
            // Remove only the partial file created by this operation.
            if (created && File.Exists(path)) File.Delete(path);
            throw;
        }
    }
}
