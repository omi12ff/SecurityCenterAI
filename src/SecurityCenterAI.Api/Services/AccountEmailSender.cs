using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SecurityCenterAI.Api.Configuration;

namespace SecurityCenterAI.Api.Services;

public enum AccountEmailPurpose { EmailVerification, PasswordReset }
public sealed record AccountEmail(string Recipient, string Token, AccountEmailPurpose Purpose);
public interface IAccountEmailSender
{
    Task SendAsync(AccountEmail message, CancellationToken cancellationToken);
}

public sealed class SmtpAccountEmailSender(IOptions<EmailOptions> options, IConfiguration configuration) : IAccountEmailSender
{
    public async Task SendAsync(AccountEmail message, CancellationToken cancellationToken)
    {
        var verification = message.Purpose == AccountEmailPurpose.EmailVerification;
        var baseUrl = (configuration["FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/');
        var link = $"{baseUrl}/{(verification ? "verify-email" : "reset-password")}?token={Uri.EscapeDataString(message.Token)}";
        using var mail = new MailMessage(options.Value.From, message.Recipient)
        {
            Subject = verification ? "Confirmá tu correo — Security Center AI" : "Restablecé tu contraseña — Security Center AI",
            Body = $"{(verification ? "Para confirmar tu correo" : "Para elegir una nueva contraseña")}, abrí este enlace:\n\n{link}\n\nSi no solicitaste este mensaje, podés ignorarlo. El enlace es de un solo uso y tiene vencimiento.",
            IsBodyHtml = false
        };
        using var client = new SmtpClient(options.Value.Host, options.Value.Port)
        {
            EnableSsl = options.Value.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrWhiteSpace(options.Value.Username) ? null :
                new NetworkCredential(options.Value.Username, options.Value.Password),
            Timeout = 10_000
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await client.SendMailAsync(mail, timeout.Token);
    }
}
