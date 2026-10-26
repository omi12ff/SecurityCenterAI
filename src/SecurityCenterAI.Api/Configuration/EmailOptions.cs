using System.Net;
using System.Net.Mail;

namespace SecurityCenterAI.Api.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1025;
    public string From { get; init; } = "noreply@securitycenter.local";
    public string? Username { get; init; }
    public string? Password { get; init; }
    public bool EnableSsl { get; init; }
    public int DeliveryAttempts { get; init; } = 3;
    public int RetryDelaySeconds { get; init; } = 1;
    public int QueueLifetimeMinutes { get; init; } = 60;

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        var section = configuration.GetSection(SectionName);
        var options = section.Get<EmailOptions>() ?? new EmailOptions();
        if (string.IsNullOrWhiteSpace(options.Host) || options.Port is < 1 or > 65535 ||
            options.DeliveryAttempts is < 1 or > 5 || options.RetryDelaySeconds is < 1 or > 30 ||
            options.QueueLifetimeMinutes is < 1 or > 1440 ||
            !MailAddress.TryCreate(options.From, out var from) || from.Address != options.From)
            throw new InvalidOperationException("La configuración de correo no es válida.");
        var frontend = configuration["FrontendUrl"] ?? "http://localhost:3000";
        if (!Uri.TryCreate(frontend, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("FrontendUrl debe ser una URL absoluta válida.");
        var local = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        if (local)
        {
            if (!(options.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                IPAddress.TryParse(options.Host, out var ip) && IPAddress.IsLoopback(ip)))
                throw new InvalidOperationException("El correo de desarrollo debe usar el capturador SMTP local.");
        }
        else if (string.IsNullOrWhiteSpace(section["Host"]) || string.IsNullOrWhiteSpace(section["From"]) ||
            string.IsNullOrWhiteSpace(options.Username) || string.IsNullOrWhiteSpace(options.Password) ||
            !options.EnableSsl || uri.Scheme != "https")
        {
            throw new InvalidOperationException("Configura SMTP autenticado con TLS y FrontendUrl HTTPS fuera de desarrollo.");
        }
    }
}
