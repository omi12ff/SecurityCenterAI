using System.Text.Json;
using SecurityCenterAI.Agent.Checks;

namespace SecurityCenterAI.Agent.Tests;

public sealed class WindowsCheckRunnerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Run_ProducesTenVersionedResultsWithEvidenceAndNoRawProviderData()
    {
        var probe = new StubProbe(kind => new ProbeResult(ProbeOutcome.Success, HealthyJson(kind)));
        var runner = new WindowsCheckRunner(new FixedClock(Now), probe);

        var report = await runner.RunAsync("0.1.0-test", CancellationToken.None);

        Assert.Equal(2, report.SchemaVersion);
        Assert.NotEqual(Guid.Empty, report.ReportId);
        Assert.Equal("0.1.0-test", report.AgentVersion);
        Assert.Equal(Now, report.StartedAtUtc);
        Assert.Equal(Now, report.CompletedAtUtc);
        Assert.Equal(10, report.Checks.Count);
        Assert.Equal(10, report.Checks.Select(result => result.RuleId).Distinct().Count());
        Assert.All(report.Checks, result =>
        {
            Assert.Equal(1, result.RuleVersion);
            Assert.Equal(CheckStatus.Passed, result.Status);
            Assert.False(string.IsNullOrWhiteSpace(result.Evidence));
            Assert.False(string.IsNullOrWhiteSpace(result.Recommendation));
            Assert.Equal(Now, result.ObservedAtUtc);
        });
        Assert.Equal(10, probe.CallCount);
        Assert.False(JsonSerializer.Serialize(report).Contains("provider-only-private-data", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(WindowsProbeKind.DefenderAntivirus, "{\"antivirusEnabled\":false,\"serviceEnabled\":true}")]
    [InlineData(WindowsProbeKind.DefenderRealtime, "{\"realtimeEnabled\":false}")]
    [InlineData(WindowsProbeKind.FirewallProfiles, "{\"domain\":true,\"private\":true,\"public\":false}")]
    [InlineData(WindowsProbeKind.SecureBoot, "{\"enabled\":false}")]
    [InlineData(WindowsProbeKind.Tpm, "{\"present\":true,\"ready\":false}")]
    [InlineData(WindowsProbeKind.BitLocker, "{\"protectionStatus\":0,\"volumeStatus\":1,\"encryptionPercentage\":100}")]
    [InlineData(WindowsProbeKind.WindowsUpdateService, "{\"startMode\":\"Disabled\",\"state\":\"Stopped\"}")]
    [InlineData(WindowsProbeKind.Uac, "{\"enabled\":0}")]
    [InlineData(WindowsProbeKind.Smb1, "{\"enabled\":true}")]
    public async Task Run_IdentifiesObservedRiskWithoutDiscardingTheOtherResults(WindowsProbeKind kind, string json)
    {
        var runner = RunnerWithOverride(kind, new ProbeResult(ProbeOutcome.Success, json));

        var report = await runner.RunAsync("0.1.0-test", CancellationToken.None);

        Assert.Equal(10, report.Checks.Count);
        var attention = Assert.Single(report.Checks.Where(result => result.Status == CheckStatus.Attention));
        Assert.Contains(attention.Severity, new[] { "medium", "high" });
        Assert.Equal(9, report.Checks.Count(result => result.Status == CheckStatus.Passed));
    }

    [Theory]
    [InlineData(ProbeOutcome.Unavailable)]
    [InlineData(ProbeOutcome.AccessDenied)]
    [InlineData(ProbeOutcome.Timeout)]
    [InlineData(ProbeOutcome.InvalidOutput)]
    public async Task Run_DoesNotTreatMissingInformationAsPassedAndPreservesPartialResults(ProbeOutcome outcome)
    {
        var runner = RunnerWithOverride(WindowsProbeKind.SecureBoot, new ProbeResult(outcome));

        var report = await runner.RunAsync("0.1.0-test", CancellationToken.None);

        Assert.Equal(CheckStatus.Unknown, Result(report, "WIN-SECURE-BOOT").Status);
        Assert.Equal(9, report.Checks.Count(result => result.Status == CheckStatus.Passed));
        Assert.Equal(10, report.Checks.Count);
    }

    [Fact]
    public async Task Run_RecordsExplicitlyUnsupportedChecksAsNotApplicable()
    {
        var runner = RunnerWithOverride(WindowsProbeKind.SecureBoot, new ProbeResult(ProbeOutcome.NotApplicable));

        var report = await runner.RunAsync("0.1.0-test", CancellationToken.None);

        Assert.Equal(CheckStatus.NotApplicable, Result(report, "WIN-SECURE-BOOT").Status);
        Assert.Equal(9, report.Checks.Count(result => result.Status == CheckStatus.Passed));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"enabled\":\"true\"}")]
    [InlineData("{\"enabled\":1}")]
    [InlineData("{\"enabled\":null}")]
    [InlineData("not-json")]
    public async Task Run_UsesUnknownForMalformedMissingOrWronglyTypedEvidence(string json)
    {
        var runner = RunnerWithOverride(WindowsProbeKind.SecureBoot, new ProbeResult(ProbeOutcome.Success, json));

        var report = await runner.RunAsync("0.1.0-test", CancellationToken.None);

        Assert.Equal(CheckStatus.Unknown, Result(report, "WIN-SECURE-BOOT").Status);
    }

    [Fact]
    public async Task Run_RejectsOversizedEvidenceInsteadOfIncludingItInTheReport()
    {
        var value = new string('x', 9000);
        var runner = RunnerWithOverride(WindowsProbeKind.SecureBoot,
            new ProbeResult(ProbeOutcome.Success, $"{{\"enabled\":true,\"extra\":\"{value}\"}}"));

        var report = await runner.RunAsync("0.1.0-test", CancellationToken.None);

        Assert.Equal(CheckStatus.Unknown, Result(report, "WIN-SECURE-BOOT").Status);
        Assert.False(JsonSerializer.Serialize(report).Contains(value, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0, CheckStatus.Passed)]
    [InlineData(1, CheckStatus.Attention)]
    public async Task Signatures_RespectTheExactSevenDayBoundary(int secondsBeyondSevenDays, CheckStatus expected)
    {
        var updated = Now.AddDays(-7).AddSeconds(-secondsBeyondSevenDays);
        var runner = RunnerWithOverride(WindowsProbeKind.DefenderSignatures, new ProbeResult(ProbeOutcome.Success,
            JsonSerializer.Serialize(new { signatureUpdatedAtUtc = updated })));

        var report = await runner.RunAsync("0.1.0-test", CancellationToken.None);

        Assert.Equal(expected, Result(report, "WIN-DEFENDER-SIGNATURES").Status);
    }

    [Theory]
    [InlineData("2026-10-21T15:05:01Z")]
    [InlineData("1999-12-31T23:59:59Z")]
    [InlineData("2026-10-21T15:00:00+03:00")]
    [InlineData("not-a-date")]
    public async Task Signatures_DoNotPassImplausibleOrNonUtcTimestamps(string updated)
    {
        var runner = RunnerWithOverride(WindowsProbeKind.DefenderSignatures, new ProbeResult(ProbeOutcome.Success,
            JsonSerializer.Serialize(new { signatureUpdatedAtUtc = updated })));

        var report = await runner.RunAsync("0.1.0-test", CancellationToken.None);

        Assert.Equal(CheckStatus.Unknown, Result(report, "WIN-DEFENDER-SIGNATURES").Status);
    }

    [Fact]
    public async Task BitLocker_DoesNotPassAnUnknownProtectionStatusOrAPartiallyEncryptedVolume()
    {
        var unknown = RunnerWithOverride(WindowsProbeKind.BitLocker, new ProbeResult(ProbeOutcome.Success,
            "{\"protectionStatus\":2,\"volumeStatus\":1,\"encryptionPercentage\":100}"));
        var partial = RunnerWithOverride(WindowsProbeKind.BitLocker, new ProbeResult(ProbeOutcome.Success,
            "{\"protectionStatus\":1,\"volumeStatus\":2,\"encryptionPercentage\":42}"));

        Assert.Equal(CheckStatus.Unknown,
            Result(await unknown.RunAsync("0.1.0-test", CancellationToken.None), "WIN-BITLOCKER-SYSTEM").Status);
        Assert.Equal(CheckStatus.Attention,
            Result(await partial.RunAsync("0.1.0-test", CancellationToken.None), "WIN-BITLOCKER-SYSTEM").Status);
    }

    [Fact]
    public async Task WindowsUpdate_AllowsManualStoppedServiceWithoutClaimingPatchesAreCurrent()
    {
        var runner = RunnerWithOverride(WindowsProbeKind.WindowsUpdateService, new ProbeResult(ProbeOutcome.Success,
            "{\"startMode\":\"Manual\",\"state\":\"Stopped\"}"));

        var report = await runner.RunAsync("0.1.0-test", CancellationToken.None);
        var result = Result(report, "WIN-UPDATE-SERVICE");

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.Contains("no se comprobó si faltan parches", result.Evidence);
    }

    [Fact]
    public async Task Run_PropagatesCancellationAndDoesNotStartAnotherProbe()
    {
        using var cancellation = new CancellationTokenSource();
        var probe = new StubProbe(kind =>
        {
            cancellation.Cancel();
            return new ProbeResult(ProbeOutcome.Success, HealthyJson(kind));
        });
        var runner = new WindowsCheckRunner(new FixedClock(Now), probe);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync("0.1.0-test", cancellation.Token));

        Assert.Equal(1, probe.CallCount);
    }

    private static WindowsCheckRunner RunnerWithOverride(WindowsProbeKind kind, ProbeResult result) =>
        new(new FixedClock(Now), new StubProbe(current => current == kind ? result : new ProbeResult(ProbeOutcome.Success, HealthyJson(current))));

    private static CheckResult Result(AgentReport report, string ruleId) =>
        Assert.Single(report.Checks.Where(result => result.RuleId == ruleId));

    private static string HealthyJson(WindowsProbeKind kind) => kind switch
    {
        WindowsProbeKind.DefenderAntivirus or WindowsProbeKind.DefenderRealtime or WindowsProbeKind.DefenderSignatures =>
            "{\"antivirusEnabled\":true,\"serviceEnabled\":true,\"realtimeEnabled\":true,\"mode\":\"Normal\",\"signatureUpdatedAtUtc\":\"2026-10-21T14:00:00Z\"}",
        WindowsProbeKind.FirewallProfiles => "{\"domain\":true,\"private\":true,\"public\":true}",
        WindowsProbeKind.SecureBoot => "{\"enabled\":true,\"ignored\":\"provider-only-private-data\"}",
        WindowsProbeKind.Tpm => "{\"present\":true,\"ready\":true}",
        WindowsProbeKind.BitLocker => "{\"protectionStatus\":1,\"volumeStatus\":1,\"encryptionPercentage\":100}",
        WindowsProbeKind.WindowsUpdateService => "{\"startMode\":\"Auto\",\"state\":\"Running\"}",
        WindowsProbeKind.Uac => "{\"enabled\":1}",
        WindowsProbeKind.Smb1 => "{\"enabled\":false}",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private sealed class StubProbe(Func<WindowsProbeKind, ProbeResult> callback) : IWindowsProbe
    {
        public int CallCount { get; private set; }

        public Task<ProbeResult> ProbeAsync(WindowsProbeKind kind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(callback(kind));
        }
    }
}
