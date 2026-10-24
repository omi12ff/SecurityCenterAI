using System.Threading.Channels;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Services.Operations;

namespace SecurityCenterAI.Api.Services;

// Plaintext delivery material is held only in a bounded in-memory queue, never in DB/logs.
public sealed record PendingAccountEmail(AccountEmail Message, DateTimeOffset QueuedAtUtc);

public sealed class QueuedAccountEmailSender(Channel<PendingAccountEmail> queue, TimeProvider clock) : IAccountEmailSender
{
    public Task SendAsync(AccountEmail message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!queue.Writer.TryWrite(new(message, clock.GetUtcNow()))) throw new InvalidOperationException("El transporte de correo está ocupado.");
        return Task.CompletedTask;
    }
}

public sealed class AccountEmailWorker(Channel<PendingAccountEmail> queue, IAccountEmailTransport transport,
    IOptions<EmailOptions> settings, TimeProvider clock, ILogger<AccountEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var pending in queue.Reader.ReadAllAsync(stoppingToken))
            {
                for (var attempt = 1; attempt <= settings.Value.DeliveryAttempts; attempt++)
                {
                    if (clock.GetUtcNow() - pending.QueuedAtUtc >= TimeSpan.FromMinutes(settings.Value.QueueLifetimeMinutes))
                    {
                        logger.LogWarning(SafeOperationalLogging.EmailDeliveryFailed, "Se descartó un correo por el límite de espera en memoria.");
                        break;
                    }
                    try { await transport.SendAsync(pending.Message, stoppingToken); break; }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception exception)
                    {
                        if (attempt >= settings.Value.DeliveryAttempts || !Transient(exception))
                        {
                            logger.LogWarning(SafeOperationalLogging.EmailDeliveryFailed, "No se pudo entregar un correo de cuenta. Revisá el transporte SMTP.");
                            break;
                        }
                        logger.LogWarning(SafeOperationalLogging.EmailDeliveryRetry, "Reintento SMTP pendiente; intento completado {Attempt}.", attempt);
                        await Task.Delay(TimeSpan.FromSeconds(settings.Value.RetryDelaySeconds * attempt), clock, stoppingToken);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private static bool Transient(Exception exception) => exception is OperationCanceledException ||
        exception is SmtpException { StatusCode: SmtpStatusCode.GeneralFailure or SmtpStatusCode.ServiceNotAvailable or
            SmtpStatusCode.MailboxBusy or SmtpStatusCode.LocalErrorInProcessing or SmtpStatusCode.InsufficientStorage };
}
