using System.Text.Json;
using System.Text.Json.Nodes;
using SecurityCenterAI.Agent.Checks;

namespace SecurityCenterAI.Agent.Tests;

public sealed class LocalReportReaderTests
{
    [Fact]
    public async Task ReadLatest_PreservesTheReportIdentifierForAnIdempotentRetry()
    {
        using var directory = new ReportDirectory();
        var report = AnalysisReportSamples.Create(DateTimeOffset.UtcNow);
        var path = await LocalReportWriter.WriteAsync(directory.Path, report, CancellationToken.None);
        var before = await File.ReadAllTextAsync(path);

        var first = await LocalReportReader.ReadLatestAsync(directory.Path, CancellationToken.None);
        var retry = await LocalReportReader.ReadLatestAsync(directory.Path, CancellationToken.None);

        Assert.Equal(report.ReportId, first.ReportId);
        Assert.Equal(report.ReportId, retry.ReportId);
        Assert.Equal(2, retry.SchemaVersion);
        Assert.Equal(10, retry.Checks.Count);
        Assert.Equal(before, await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("missing-id")]
    [InlineData("empty-id")]
    [InlineData("wrong-schema")]
    [InlineData("malformed")]
    [InlineData("oversize")]
    public async Task ReadLatest_DoesNotInventAnIdentityOrSilentlyConvertAnOldOrInvalidReport(string scenario)
    {
        using var directory = new ReportDirectory();
        var path = await LocalReportWriter.WriteAsync(directory.Path,
            AnalysisReportSamples.Create(DateTimeOffset.UtcNow), CancellationToken.None);
        var document = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        var contents = scenario switch
        {
            "legacy" => WithSchema(document, 1),
            "missing-id" => WithoutId(document),
            "empty-id" => WithId(document, Guid.Empty),
            "wrong-schema" => WithSchema(document, 999),
            "malformed" => "{incomplete-local-report",
            "oversize" => new string('x', 65 * 1024),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        await File.WriteAllTextAsync(path, contents);

        var exception = await Assert.ThrowsAsync<AgentException>(() =>
            LocalReportReader.ReadLatestAsync(directory.Path, CancellationToken.None));

        Assert.Null(exception.InnerException);
        Assert.Equal(contents, await File.ReadAllTextAsync(path));
        Assert.False(exception.ToString().Contains("incomplete-local-report", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadLatest_FailsClearlyWhenThereIsNoReport()
    {
        using var directory = new ReportDirectory();

        await Assert.ThrowsAsync<AgentException>(() => LocalReportReader.ReadLatestAsync(directory.Path, CancellationToken.None));
    }

    [Fact]
    public async Task ReadLatest_PropagatesCancellation()
    {
        using var directory = new ReportDirectory();
        await LocalReportWriter.WriteAsync(directory.Path, AnalysisReportSamples.Create(DateTimeOffset.UtcNow), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            LocalReportReader.ReadLatestAsync(directory.Path, cancellation.Token));
    }

    private static string WithSchema(JsonObject document, int schema)
    {
        document["schemaVersion"] = schema;
        return document.ToJsonString();
    }

    private static string WithoutId(JsonObject document)
    {
        document.Remove("reportId");
        return document.ToJsonString();
    }

    private static string WithId(JsonObject document, Guid reportId)
    {
        document["reportId"] = reportId;
        return document.ToJsonString();
    }

    private sealed class ReportDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "securitycenterai-agent-reader-tests-" + Guid.NewGuid().ToString("N"));

        public ReportDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            var reports = System.IO.Path.Combine(Path, "reports");
            if (Directory.Exists(reports))
            {
                foreach (var file in Directory.EnumerateFiles(reports)) File.Delete(file);
                Directory.Delete(reports);
            }
            Directory.Delete(Path);
        }
    }
}
