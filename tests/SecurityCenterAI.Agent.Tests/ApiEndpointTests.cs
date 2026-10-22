using SecurityCenterAI.Agent;
using SecurityCenterAI.Agent.Security;

namespace SecurityCenterAI.Agent.Tests;

public sealed class ApiEndpointTests
{
    [Theory]
    [InlineData("https://api.example.invalid/")]
    [InlineData("https://api.example.invalid:8443")]
    [InlineData("http://localhost:5169")]
    [InlineData("http://127.0.0.1:5169/")]
    [InlineData("http://[::1]:5169/")]
    public void Parse_AcceptsHttpsAndExactDevelopmentLoopback(string address)
    {
        var endpoint = ApiEndpoint.Parse(address);

        Assert.True(endpoint.IsAbsoluteUri);
        Assert.Equal("/", endpoint.AbsolutePath);
    }

    [Theory]
    [InlineData("http://192.168.1.20:5169/")]
    [InlineData("http://api.example.invalid/")]
    [InlineData("http://127.0.0.2:5169/")]
    [InlineData("http://localhost.example.invalid:5169/")]
    [InlineData("https://operator:password@api.example.invalid/")]
    [InlineData("https://api.example.invalid/?code=private")]
    [InlineData("https://api.example.invalid/#private")]
    [InlineData("https://api.example.invalid/api/")]
    [InlineData("file:///C:/api")]
    [InlineData("/api/agent")]
    [InlineData("")]
    public void Parse_RejectsUnsafeOrAmbiguousDestinationsWithoutEchoingTheAddress(string address)
    {
        var exception = Assert.Throws<AgentException>(() => ApiEndpoint.Parse(address));

        if (address.Length > 0)
            Assert.False(exception.ToString().Contains(address, StringComparison.Ordinal));
        Assert.False(exception.Message.Contains("password", StringComparison.Ordinal));
        Assert.False(exception.Message.Contains("private", StringComparison.Ordinal));
    }
}
