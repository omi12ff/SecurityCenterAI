namespace SecurityCenterAI.Agent.Security;

public static class ApiEndpoint
{
    public static Uri Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || value.Any(char.IsControl) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/" || uri.Port < 1)
            throw Invalid();

        var host = uri.Host.Trim('[', ']');
        var local = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host == "127.0.0.1" || host == "::1";
        if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && local))
            throw Invalid();
        return uri;
    }

    private static AgentException Invalid() => new(
        "La API debe usar una URL raíz HTTPS. HTTP solo se admite en localhost, 127.0.0.1 o [::1].");
}
