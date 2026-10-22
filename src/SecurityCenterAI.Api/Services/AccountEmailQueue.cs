using System.Threading.Channels;

namespace SecurityCenterAI.Api.Services;

// Plaintext delivery material is held only in a bounded in-memory queue, never in DB/logs.
public sealed class QueuedAccountEmailSender(Channel<AccountEmail> queue) : IAccountEmailSender
{
    public Task SendAsync(AccountEmail message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!queue.Writer.TryWrite(message)) throw new InvalidOperationException("El transporte de correo está ocupado.");
        return Task.CompletedTask;
    }
}

public sealed class AccountEmailWorker(Channel<AccountEmail> queue, SmtpAccountEmailSender transport,
    ILogger<AccountEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try { await transport.SendAsync(message, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception) { logger.LogWarning("No se pudo entregar un correo de cuenta. Revisa el transporte SMTP."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
