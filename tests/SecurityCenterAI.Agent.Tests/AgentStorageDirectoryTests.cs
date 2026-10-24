namespace SecurityCenterAI.Agent.Tests;

public sealed class AgentStorageDirectoryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative")]
    [InlineData("C:relative")]
    public void UndefinedOrRelativeLocation_NeverFallsBackToThePackageDirectory(string? location)
    {
        var error = Assert.Throws<AgentException>(() => AgentStorageDirectory.Resolve(location));
        Assert.Contains("No se guardará estado en la carpeta del agente", error.Message);
    }

    [Fact]
    public void AbsoluteLocation_RetainsTheExistingCredentialAndReportDirectory()
    {
        var localData = Path.Combine(Path.GetTempPath(), $"isolated-user-{Guid.NewGuid():N}");
        Assert.Equal(Path.Combine(localData, "SecurityCenterAI", "Agent"), AgentStorageDirectory.Resolve(localData));
        Assert.False(Directory.Exists(Path.Combine(localData, "SecurityCenterAI")));
    }
}
