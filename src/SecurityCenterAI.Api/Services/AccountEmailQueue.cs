using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Services.Operations;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services;

public sealed class QueuedAccountEmailSender(AppDbContext database, AccountEmailProtection protection,
    TimeProvider clock, IOptions<EmailOptions> settings) : IAccountEmailSender
{
    public Task QueueAsync(Guid userId, AccountEmail message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (userId == Guid.Empty) throw new InvalidOperationException("El correo requiere una cuenta propietaria.");
        var now = clock.GetUtcNow().UtcDateTime;
        var row = new AccountEmailOutbox { UserId = userId, CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(settings.Value.QueueLifetimeMinutes), NextAttemptAtUtc = now };
        row.ProtectedPayload = protection.Protect(userId, row.Id, message);
        if (row.ProtectedPayload.Length > 8192) throw new InvalidOperationException("El correo supera el tamaño permitido.");
        database.AccountEmailOutbox.Add(row);
        return Task.CompletedTask;
    }
}

public sealed class AccountEmailDispatcher(AppDbContext database, AccountEmailProtection protection, IAccountEmailTransport transport,
    IOptions<EmailOptions> settings, TimeProvider clock, ILogger<AccountEmailWorker> logger)
{
    // SMTP is bounded to 10 seconds. A crashed worker's reservation expires after two minutes.
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private sealed record Claim(AccountEmailOutbox Row, bool AttemptsExhausted);

    public async Task<bool> ProcessOneAsync(CancellationToken cancellationToken)
    {
        var claim = await ClaimAsync(cancellationToken);
        if (claim is null) return false;
        var row = claim.Row;
        if (row.ExpiresAtUtc <= Now || claim.AttemptsExhausted)
        { await RemoveAsync(row, cancellationToken); Failed("Se retiró un correo vencido o sin intentos disponibles."); return true; }

        AccountEmail message;
        try { message = protection.Unprotect(row.UserId, row.Id, row.ProtectedPayload); }
        catch (Exception exception) when (exception is CryptographicException or JsonException or InvalidOperationException)
        { await RemoveAsync(row, cancellationToken); Failed("Se retiró un correo cuyo contenido protegido no pudo recuperarse."); return true; }

        if (!await StillRelevantAsync(row.UserId, message, cancellationToken) ||
            !await database.AccountEmailOutbox.AsNoTracking().AnyAsync(item => item.Id == row.Id &&
                item.LeaseId == row.LeaseId && item.LeaseExpiresAtUtc > Now && item.ExpiresAtUtc > Now, cancellationToken))
        { await RemoveAsync(row, cancellationToken); return true; }

        try
        {
            await transport.SendAsync(message, cancellationToken);
            await RemoveAsync(row, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Retain the encrypted row and reservation on shutdown. A restart can reclaim it.
            throw;
        }
        catch (Exception exception)
        {
            if (row.Attempts >= settings.Value.DeliveryAttempts || !Transient(exception))
            {
                await RemoveAsync(row, cancellationToken);
                Failed("No se pudo entregar un correo de cuenta. Revisá el transporte SMTP.");
            }
            else
            {
                row.NextAttemptAtUtc = Now.AddSeconds(settings.Value.RetryDelaySeconds * row.Attempts);
                row.LeaseId = null; row.LeaseExpiresAtUtc = null; row.ConcurrencyStamp = Guid.NewGuid();
                try { await database.SaveChangesAsync(cancellationToken); }
                catch (DbUpdateConcurrencyException) { database.ChangeTracker.Clear(); }
                logger.LogWarning(SafeOperationalLogging.EmailDeliveryRetry,
                    "Reintento SMTP pendiente; intento completado {Attempt}.", row.Attempts);
            }
        }
        return true;
    }

    private async Task<Claim?> ClaimAsync(CancellationToken cancellationToken)
    {
        var now = Now;
        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(cancellationToken) : null;
        var row = database.Database.IsRelational()
            ? (await database.AccountEmailOutbox.FromSqlInterpolated($"""
                SELECT * FROM account_email_outbox
                WHERE (fecha_expiracion_utc <= {now} OR fecha_proximo_intento_utc <= {now})
                  AND (id_reserva IS NULL OR fecha_fin_reserva_utc <= {now})
                ORDER BY (fecha_expiracion_utc <= {now}) DESC, fecha_proximo_intento_utc, id_correo
                FOR UPDATE SKIP LOCKED LIMIT 1
                """).ToListAsync(cancellationToken)).SingleOrDefault()
            : await database.AccountEmailOutbox.Where(item => (item.ExpiresAtUtc <= now || item.NextAttemptAtUtc <= now) &&
                (item.LeaseId == null || item.LeaseExpiresAtUtc <= now))
                .OrderBy(item => item.ExpiresAtUtc > now).ThenBy(item => item.NextAttemptAtUtc).ThenBy(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        var exhausted = row.Attempts >= settings.Value.DeliveryAttempts;
        row.LeaseId = Guid.NewGuid(); row.LeaseExpiresAtUtc = now.Add(LeaseDuration); row.ConcurrencyStamp = Guid.NewGuid();
        if (!exhausted) row.Attempts++;
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new(row, exhausted);
        }
        catch (DbUpdateConcurrencyException) { database.ChangeTracker.Clear(); return null; }
    }

    private async Task<bool> StillRelevantAsync(Guid userId, AccountEmail message, CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null) return false;
        var now = Now;
        if (message.Purpose is AccountEmailPurpose.EmailVerification or AccountEmailPurpose.PasswordReset)
        {
            if (user.Email != message.Recipient || message.Purpose == AccountEmailPurpose.EmailVerification && user.EmailVerified) return false;
            var hash = OpaqueToken.Hash(message.Token);
            var purpose = message.Purpose == AccountEmailPurpose.EmailVerification ? AccountService.VerificationPurpose : AccountService.PasswordResetPurpose;
            return await database.AccountActionTokens.AsNoTracking().AnyAsync(item => item.UserId == userId &&
                item.TokenHash == hash && item.Purpose == purpose && item.ConsumedAtUtc == null && item.ExpiresAtUtc > now, cancellationToken);
        }
        if (message.Purpose is AccountEmailPurpose.EmailChangeCurrent or AccountEmailPurpose.EmailChangeNew)
        {
            var hash = OpaqueToken.Hash(message.Token);
            return await database.EmailChangeRequests.AsNoTracking().AnyAsync(item => item.UserId == userId &&
                item.CurrentEmail == user.Email && item.ExpiresAtUtc > now &&
                (message.Purpose == AccountEmailPurpose.EmailChangeCurrent
                    ? item.CurrentTokenHash == hash && item.CurrentEmail == message.Recipient && item.CurrentConfirmedAtUtc == null
                    : item.NewTokenHash == hash && item.NewEmail == message.Recipient && item.NewConfirmedAtUtc == null), cancellationToken);
        }
        return true;
    }

    private async Task RemoveAsync(AccountEmailOutbox row, CancellationToken cancellationToken)
    {
        database.AccountEmailOutbox.Remove(row);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { database.ChangeTracker.Clear(); }
    }

    private void Failed(string message) => logger.LogWarning(SafeOperationalLogging.EmailDeliveryFailed, "{DeliveryStatus}", message);
    private static bool Transient(Exception exception) => exception is OperationCanceledException ||
        exception is SmtpException { StatusCode: SmtpStatusCode.GeneralFailure or SmtpStatusCode.ServiceNotAvailable or
            SmtpStatusCode.MailboxBusy or SmtpStatusCode.LocalErrorInProcessing or SmtpStatusCode.InsufficientStorage };
}

public sealed class AccountEmailWorker(IServiceScopeFactory scopes, AccountEmailProtection protection,
    TimeProvider clock, ILogger<AccountEmailWorker> logger) : BackgroundService
{
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var probe = new AccountEmail("probe@example.invalid", "", AccountEmailPurpose.PasswordChanged);
        protection.Unprotect(Guid.Empty, Guid.Empty, protection.Protect(Guid.Empty, Guid.Empty, probe));
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var processed = await scope.ServiceProvider.GetRequiredService<AccountEmailDispatcher>().ProcessOneAsync(stoppingToken);
                    if (!processed) await Task.Delay(TimeSpan.FromSeconds(2), clock, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception)
                {
                    logger.LogWarning(SafeOperationalLogging.EmailDeliveryFailed, "No se pudo consultar o confirmar la cola persistente de correo.");
                    await Task.Delay(TimeSpan.FromSeconds(5), clock, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
