using System.Text.RegularExpressions;

namespace SecurityCenterAI.Api.Configuration;

public sealed class AiExplanationOptions
{
    public const string SectionName = "AiExplanation";
    public bool Enabled { get; set; }
    public string GatewayBaseUrl { get; set; } = "http://127.0.0.1:20128/v1/";
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ApiKeyFile { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
    public int DailyLimit { get; set; } = 5;

    public static string LocalDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SecurityCenterAI", "Api", "AiExplanation");
    public static bool ValidToken(string? value) => value is { Length: >= 16 and <= 4096 } && value.All(c => c is >= '!' and <= '~');
    public static bool FreeModel(string? value) => value == "openrouter/openrouter/free" || value is { Length: <= 200 } &&
        Regex.IsMatch(value, @"\Aopenrouter/[a-z0-9][a-z0-9._-]*/[a-z0-9][a-z0-9._-]*:free\z", RegexOptions.CultureInvariant);
    public bool TryGateway(out Uri gateway)
    {
        gateway = null!;
        if (!Uri.TryCreate(GatewayBaseUrl, UriKind.Absolute, out var parsed) || !string.IsNullOrEmpty(parsed.UserInfo) ||
            !string.IsNullOrEmpty(parsed.Query) || !string.IsNullOrEmpty(parsed.Fragment) || parsed.AbsolutePath.TrimEnd('/') != "/v1") return false;
        var local = parsed.Host is "127.0.0.1" or "localhost" or "[::1]";
        if (parsed.Scheme != Uri.UriSchemeHttps && !(parsed.Scheme == Uri.UriSchemeHttp && local)) return false;
        gateway = new Uri(parsed.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/");
        return true;
    }
    public bool IsConfigured() => TimeoutSeconds is >= 5 and <= 60 && DailyLimit is >= 1 and <= 50 &&
        TryGateway(out _) && FreeModel(Model) && (ValidToken(ApiKey) || ValidKeyFile());

    public bool ValidKeyFile()
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(ApiKeyFile)) return false;
        try
        {
            var expected = Path.Combine(LocalDirectory, "gateway-key.dpapi");
            if (!Path.GetFullPath(ApiKeyFile).Equals(Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase)) return false;
            var file = new FileInfo(expected);
            if (!file.Exists || file.Length is < 1 or > 16384 || file.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return false; }
    }
}
