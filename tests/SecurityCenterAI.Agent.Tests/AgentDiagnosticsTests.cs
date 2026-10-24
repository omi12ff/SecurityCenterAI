using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace SecurityCenterAI.Agent.Tests;

public sealed class AgentDiagnosticsTests
{
    private static DiagnosticEnvironment Supported => new(true, new Version(10, 0, 26300),
        Architecture.X64, Architecture.X64, true, true, false, ".NET 8.0-test");

    [Fact]
    public void BasicRequirements_DoNotCertifySecurityOrProbeAccess()
    {
        var report = AgentDiagnostics.Evaluate(Supported, "0.4.0-test");
        Assert.True(report.BasicRequirementsAvailable);
        Assert.Equal(5, report.Checks.Count);
        Assert.Contains(report.Limits, limit => limit.Contains("no confirma sus políticas"));
        Assert.Contains(report.Limits, limit => limit.Contains("no garantizan compatibilidad"));
        using var output = new StringWriter();
        AgentDiagnostics.Print(report, output);
        Assert.Contains("sin elevación", output.ToString());
        Assert.DoesNotContain("Revisá los permisos", output.ToString());
    }

    [Theory]
    [InlineData("windows-build")]
    [InlineData("os-x64")]
    [InlineData("process-x64")]
    [InlineData("system-powershell")]
    [InlineData("local-data-location")]
    public void MissingPrerequisite_IsExplicitWithoutInventingProtectionState(string id)
    {
        var environment = id switch
        {
            "windows-build" => Supported with { OperatingSystemVersion = new Version(10, 0, 19045) },
            "os-x64" => Supported with { OperatingSystemArchitecture = Architecture.Arm64 },
            "process-x64" => Supported with { ProcessArchitecture = Architecture.X86 },
            "system-powershell" => Supported with { SystemPowerShellAvailable = false },
            _ => Supported with { LocalDataLocationAvailable = false }
        };
        var report = AgentDiagnostics.Evaluate(environment, "test");
        Assert.False(report.BasicRequirementsAvailable);
        Assert.Equal(id, Assert.Single(report.Checks.Where(check => !check.Available)).Id);
    }

    [Fact]
    public void OtherPlatform_ReportsUnavailabilityAndUnknownElevation()
    {
        var report = AgentDiagnostics.Evaluate(Supported with { IsWindows = false, IsElevated = null }, "test");
        Assert.False(report.BasicRequirementsAvailable);
        Assert.Equal("Otro", report.OperatingSystem);
        using var output = new StringWriter();
        AgentDiagnostics.Print(report, output);
        Assert.Contains("No se pudo determinar la elevación", output.ToString());
    }

    [Fact]
    public void ElevatedTerminal_IsReportedWithoutRequestingElevation()
    {
        using var output = new StringWriter();
        AgentDiagnostics.Print(AgentDiagnostics.Evaluate(Supported with { IsElevated = true }, "test"), output);
        Assert.Contains("abrí una terminal normal", output.ToString());
    }

    [Fact]
    public void DiagnosticSchema_ContainsOnlySelectedNonIdentifyingMetadata()
    {
        var report = AgentDiagnostics.Evaluate(Supported, "test");
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(report));
        Assert.Equal(new[] { "AgentVersion", "BasicRequirementsAvailable", "Checks", "IsElevated", "Limits", "OperatingSystem",
            "OperatingSystemArchitecture", "OperatingSystemVersion", "ProcessArchitecture", "RuntimeVersion", "SchemaVersion" },
            document.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray());
    }

    [Fact]
    public void Version_ComesFromThePackagedAssemblyWithoutGitSuffix()
    {
        var metadata = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!;
        Assert.Equal(metadata.InformationalVersion.Split('+')[0], Program.AgentVersion);
        Assert.DoesNotContain('+', Program.AgentVersion);
    }
}
