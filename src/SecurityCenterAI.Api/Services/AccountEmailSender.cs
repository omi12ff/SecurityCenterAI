using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SecurityCenterAI.Api.Configuration;

namespace SecurityCenterAI.Api.Services;

public enum AccountEmailPurpose { EmailVerification, PasswordReset, PasswordChanged, PasswordResetCompleted, EmailChangeCurrent, EmailChangeNew, EmailChanged }
public sealed record AccountEmail(string Recipient, string Token, AccountEmailPurpose Purpose);
public interface IAccountEmailSender
{
    Task SendAsync(AccountEmail message, CancellationToken cancellationToken);
}

public interface IAccountEmailTransport
{
    Task SendAsync(AccountEmail message, CancellationToken cancellationToken);
}

public sealed class SmtpAccountEmailSender(IOptions<EmailOptions> options, IConfiguration configuration) : IAccountEmailTransport
{
    public async Task SendAsync(AccountEmail message, CancellationToken cancellationToken)
    {
        var baseUrl = (configuration["FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/');
        var route = message.Purpose switch
        {
            AccountEmailPurpose.EmailVerification => "verify-email",
            AccountEmailPurpose.PasswordReset => "reset-password",
            AccountEmailPurpose.EmailChangeCurrent or AccountEmailPurpose.EmailChangeNew => "confirm-email-change",
            _ => null
        };
        var link = route is null ? "" : $"{baseUrl}/{route}?token={Uri.EscapeDataString(message.Token)}";
        if (message.Purpose == AccountEmailPurpose.EmailChangeCurrent) link += "&kind=current";
        if (message.Purpose == AccountEmailPurpose.EmailChangeNew) link += "&kind=new";
        var subject = message.Purpose switch
        {
            AccountEmailPurpose.EmailVerification => "Confirmá tu correo — Security Center AI",
            AccountEmailPurpose.PasswordReset => "Restablecé tu contraseña — Security Center AI",
            AccountEmailPurpose.PasswordChanged => "Tu contraseña fue cambiada — Security Center AI",
            AccountEmailPurpose.PasswordResetCompleted => "Tu contraseña fue restablecida — Security Center AI",
            AccountEmailPurpose.EmailChangeCurrent => "Aprobá el cambio desde tu correo actual — Security Center AI",
            AccountEmailPurpose.EmailChangeNew => "Confirmá tu correo nuevo — Security Center AI",
            AccountEmailPurpose.EmailChanged => "Tu correo fue cambiado — Security Center AI",
            _ => throw new InvalidOperationException("Tipo de correo no reconocido.")
        };
        var body = message.Purpose switch
        {
            AccountEmailPurpose.PasswordChanged => "Se cambió la contraseña de tu cuenta y se cerraron todas las sesiones web. Los agentes vinculados conservan su acceso independiente. Si no hiciste este cambio, abrí la aplicación, solicitá recuperar tu contraseña y revisá tus dispositivos. Este aviso no contiene contraseñas ni enlaces de acceso.",
            AccountEmailPurpose.PasswordResetCompleted => "Se restableció la contraseña de tu cuenta. Se cerraron las sesiones web, se invalidaron códigos y credenciales de agentes y se canceló cualquier cambio de correo pendiente. Si no hiciste esta recuperación, revisá la seguridad de tu correo y el acceso a la aplicación. Este aviso no contiene contraseñas, tokens o enlaces.",
            AccountEmailPurpose.EmailChanged => "Se completó el cambio de correo de tu cuenta. Las sesiones web fueron cerradas; iniciá sesión con el correo nuevo y tu contraseña habitual. Los equipos vinculados conservan su acceso. Si no reconocés este cambio, revisá la seguridad de tus correos y comunicá el problema al responsable de esta instalación. Este aviso no contiene enlaces o credenciales.",
            AccountEmailPurpose.EmailChangeCurrent or AccountEmailPurpose.EmailChangeNew => $"Se solicitó un cambio de correo desde Mi cuenta. Si lo pediste, abrí el enlace y confirmá esta dirección:\n\n{link}\n\nEl cambio requiere confirmar ambos correos y volver a Mi cuenta para revisar la dirección nueva e ingresar otra vez tu contraseña. Este enlace solo confirma una dirección. Si no lo solicitaste, no lo confirmes; podés cancelar desde la aplicación o recuperar tu contraseña para invalidarlo.",
            _ => $"{(message.Purpose == AccountEmailPurpose.EmailVerification ? "Para confirmar tu correo" : "Para elegir una nueva contraseña")}, abrí este enlace:\n\n{link}\n\nSi no solicitaste este mensaje, podés ignorarlo. El enlace es de un solo uso y tiene vencimiento."
        };
        using var mail = new MailMessage(options.Value.From, message.Recipient)
        {
            Subject = subject,
            Body = body,
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
