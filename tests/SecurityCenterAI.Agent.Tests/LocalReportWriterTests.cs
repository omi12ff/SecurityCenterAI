using System.Text.Json;
using SecurityCenterAI.Agent.Checks;

namespace SecurityCenterAI.Agent.Tests;

public sealed class LocalReportWriterTests
{
    [Fact]
    public async Task Write_PreservesVersionedEvidenceAndDoesNotAddIdentityCredentialsOrScores()
    {
        using var directory = new ReportDirectory();
        var now = new DateTimeOffset(2026, 10, 21, 15, 0, 0, TimeSpan.Zero);
        var report = new AgentReport(2, "0.1.0-test", now, now,
        [
            new("WIN-UAC-ENABLED", 1, "Control de cuentas", "informational", CheckStatus.Passed,
                "EnableLUA=1.", "Revisá la configuración.", now),
            new("WIN-SECURE-BOOT", 1, "Arranque seguro", "informational", CheckStatus.Unknown,
                "No se pudo consultar.", "Comprobalo manualmente.", now)
        ]);

        var path = await LocalReportWriter.WriteAsync(directory.Path, report, CancellationToken.None);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var root = json.RootElement;

        Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(report.ReportId, root.GetProperty("reportId").GetGuid());
        Assert.Equal("0.1.0-test", root.GetProperty("agentVersion").GetString());
        Assert.Equal(2, root.GetProperty("checks").GetArrayLength());
        Assert.Equal("passed", root.GetProperty("checks")[0].GetProperty("status").GetString());
        Assert.Equal("unknown", root.GetProperty("checks")[1].GetProperty("status").GetString());
        Assert.Equal("EnableLUA=1.", root.GetProperty("checks")[0].GetProperty("evidence").GetString());
        var forbiddenFields = new[] { "deviceToken", "pairingCode", "score", "userId", "deviceId", "username", "hostname", "serialNumber" };
        foreach (var field in forbiddenFields)
            Assert.False(HasProperty(root, field));
    }

    [Fact]
    public async Task Write_DoesNotOverwriteAnEarlierReportWithTheSameStartTime()
    {
        using var directory = new ReportDirectory();
        var now = new DateTimeOffset(2026, 10, 21, 15, 0, 0, TimeSpan.Zero);
        var firstReport = new AgentReport(1, "first", now, now, []);
        var secondReport = new AgentReport(1, "second", now, now, []);

        var firstPath = await LocalReportWriter.WriteAsync(directory.Path, firstReport, CancellationToken.None);
        var firstContents = await File.ReadAllTextAsync(firstPath);
        var secondPath = await LocalReportWriter.WriteAsync(directory.Path, secondReport, CancellationToken.None);

        Assert.NotEqual(firstPath, secondPath);
        Assert.Equal(firstContents, await File.ReadAllTextAsync(firstPath));
        Assert.Equal(2, Directory.GetFiles(System.IO.Path.Combine(directory.Path, "reports")).Length);
    }

    [Fact]
    public async Task Write_DoesNotLeaveAPartialReportWhenCancelled()
    {
        using var directory = new ReportDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var now = new DateTimeOffset(2026, 10, 21, 15, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => LocalReportWriter.WriteAsync(directory.Path,
            new AgentReport(1, "0.1.0-test", now, now, []), cancellation.Token));

        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*", SearchOption.AllDirectories));
    }

    private static bool HasProperty(JsonElement element, string name) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Any(property =>
            property.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || HasProperty(property.Value, name)),
        JsonValueKind.Array => element.EnumerateArray().Any(item => HasProperty(item, name)),
        _ => false
    };

    private sealed class ReportDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "securitycenterai-agent-report-tests-" + Guid.NewGuid().ToString("N"));

        public ReportDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            var reportsDirectory = System.IO.Path.Combine(Path, "reports");
            if (Directory.Exists(reportsDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(reportsDirectory))
                    File.Delete(file);
                Directory.Delete(reportsDirectory);
            }
            Directory.Delete(Path);
        }
    }
}
