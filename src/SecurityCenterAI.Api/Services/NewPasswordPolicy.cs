using System.Text;

namespace SecurityCenterAI.Api.Services;

// Initial list of predictable values, not a complete breach corpus.
// Candidates never leave the API and must not appear in errors or logs.
public static class NewPasswordPolicy
{
    public const int MinimumLength = 15;
    public const int MaximumLength = 128;
    public const string Message = "Usá entre 15 y 128 caracteres y evitá contraseñas comunes o previsibles.";
    private static readonly HashSet<string> Predictable = new(StringComparer.OrdinalIgnoreCase)
    {
        "passwordpassword", "password123456789", "password123456!", "password12345678",
        "123456789012345", "1234567890123456", "12345678901234567890",
        "qwertyuiopasdfgh", "qwertyuiop123456", "qwertyuiopasdfghjkl",
        "letmeinletmeinletmein", "iloveyouiloveyou", "welcome123456789",
        "changemechangeme", "contraseñacontraseña", "contrasenacontrasena",
        "securitycenterai", "securitycenterai123", "securitycenterai2026",
        "aaaaaaaaaaaaaaa", "aaaaaaaaaaaaaaaa", "111111111111111", "1111111111111111"
    };

    public static bool Accepts(string? candidate) => !string.IsNullOrWhiteSpace(candidate) &&
        candidate.Length <= MaximumLength && candidate.EnumerateRunes().Count() >= MinimumLength &&
        !Predictable.Contains(candidate);

    public static void Require(string? candidate)
    {
        if (!Accepts(candidate)) throw new NewPasswordRejectedException();
    }
}

public sealed class NewPasswordRejectedException() : Exception(NewPasswordPolicy.Message);
