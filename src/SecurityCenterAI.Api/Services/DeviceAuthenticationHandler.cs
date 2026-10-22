using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services;

public sealed class DeviceAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, AppDbContext database, TimeProvider clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Device";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Device ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();
        var token = header[7..];
        if (!OpaqueToken.IsValid(token)) return AuthenticateResult.Fail("La credencial del dispositivo no es válida.");
        var hash = OpaqueToken.Hash(token);
        var credential = await database.DeviceCredentials.AsNoTracking().Include(item => item.Device)
            .SingleOrDefaultAsync(item => item.TokenHash == hash, Context.RequestAborted);
        if (credential is null || credential.RevokedAtUtc.HasValue || credential.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime || credential.Device.RevokedAtUtc.HasValue)
            return AuthenticateResult.Fail("La credencial del dispositivo no es válida.");
        var identity = new ClaimsIdentity(new[] { new Claim("device_id", credential.DeviceId.ToString()),
            new Claim("credential_id", credential.Id.ToString()) }, SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
