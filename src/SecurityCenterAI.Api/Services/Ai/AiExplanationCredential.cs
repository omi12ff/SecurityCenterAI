using System.Security.Cryptography;
using System.Text;
using SecurityCenterAI.Api.Configuration;

namespace SecurityCenterAI.Api.Services.Ai;

public sealed class AiExplanationCredential
{
    public async Task<string?> ReadAsync(AiExplanationOptions options, CancellationToken cancellationToken)
    {
        if (AiExplanationOptions.ValidToken(options.ApiKey)) return options.ApiKey;
        if (!OperatingSystem.IsWindows() || !options.ValidKeyFile()) return null;
        byte[]? encrypted = null, clear = null;
        try
        {
            await using var file = new FileStream(options.ApiKeyFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length is < 1 or > 16384) return null;
            encrypted = new byte[(int)file.Length];
            await file.ReadExactlyAsync(encrypted, cancellationToken);
            clear = ProtectedData.Unprotect(encrypted, Encoding.UTF8.GetBytes("SecurityCenterAI.AiExplanation.v1"), DataProtectionScope.CurrentUser);
            var token = new UTF8Encoding(false, true).GetString(clear);
            return AiExplanationOptions.ValidToken(token) ? token : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or DecoderFallbackException)
        { return null; }
        finally
        {
            if (encrypted is not null) CryptographicOperations.ZeroMemory(encrypted);
            if (clear is not null) CryptographicOperations.ZeroMemory(clear);
        }
    }
}
