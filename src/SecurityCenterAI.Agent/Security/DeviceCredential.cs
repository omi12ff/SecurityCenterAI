namespace SecurityCenterAI.Agent.Security;

public sealed class DeviceCredential
{
    public DeviceCredential(Uri apiBaseUri, Guid deviceId, string deviceToken, DateTimeOffset expiresAtUtc)
    {
        ApiBaseUri = ApiEndpoint.Parse(apiBaseUri.AbsoluteUri);
        if (deviceId == Guid.Empty || !IsOpaqueSecret(deviceToken))
            throw new AgentException("La credencial del dispositivo no tiene un formato válido.");
        DeviceId = deviceId;
        DeviceToken = deviceToken;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Uri ApiBaseUri { get; }
    public Guid DeviceId { get; }
    public string DeviceToken { get; }
    public DateTimeOffset ExpiresAtUtc { get; }

    public void ValidateExpiry(TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        if (ExpiresAtUtc <= now || ExpiresAtUtc > now.AddDays(91))
            throw new AgentException("La credencial venció o su vigencia no es válida. Generá un código nuevo y volvé a vincular.");
    }

    public static bool IsOpaqueSecret(string? value) => value is { Length: 43 } &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    public override string ToString() => "Credencial de dispositivo [secreto oculto]";
}
