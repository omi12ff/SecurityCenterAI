using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services.Operations;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services;

public sealed class AccountEmailChangeService(AppDbContext database, IPasswordHasher<User> passwords,
    IOptions<AuthSecurityOptions> settings, TimeProvider clock, IAccountEmailSender email)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<EmailChangeStatusResponse> StatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await database.EmailChangeRequests.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return new(row is null ? null : new(row.Id, row.NewEmail, row.CreatedAtUtc, row.ExpiresAtUtc,
            row.CurrentConfirmedAtUtc.HasValue, row.NewConfirmedAtUtc.HasValue, row.ExpiresAtUtc <= Now));
    }

    public async Task RequestAsync(Guid userId, Guid sessionId, RequestEmailChangeRequest request, CancellationToken cancellationToken)
    {
        var target = request.NewEmail?.Trim().ToLowerInvariant() ?? "";
        if (!request.Confirmed || string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 128 ||
            target.Length > 320 || !new EmailAddressAttribute().IsValid(target))
            throw new DeviceOperationException(400, "Revisá el correo nuevo y confirmá que tenés acceso a ambas direcciones.");
        var currentToken = OpaqueToken.Create(); var newToken = OpaqueToken.Create();
        await WriteAsync(userId, sessionId, request.CurrentPassword, async user =>
        {
            if (target == user.Email || await database.Users.AnyAsync(item => item.Email == target, cancellationToken))
                throw new DeviceOperationException(409, "No se puede usar esa dirección para el cambio.");
            if (user.LastVerificationSentAtUtc > Now.AddSeconds(-settings.Value.EmailCooldownSeconds))
                throw new DeviceOperationException(429, "Esperá antes de solicitar otro cambio o enlace.");
            var pending = await database.EmailChangeRequests.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
            if (pending is null)
            {
                pending = new() { UserId = userId, CurrentEmail = user.Email, NewEmail = target,
                    CurrentTokenHash = "", NewTokenHash = "" };
                database.EmailChangeRequests.Add(pending);
            }
            pending.Id = Guid.NewGuid(); pending.CurrentEmail = user.Email; pending.NewEmail = target;
            pending.CurrentTokenHash = OpaqueToken.Hash(currentToken); pending.NewTokenHash = OpaqueToken.Hash(newToken);
            pending.CreatedAtUtc = Now; pending.ExpiresAtUtc = Now.AddMinutes(settings.Value.EmailChangeMinutes);
            pending.CurrentConfirmedAtUtc = null; pending.NewConfirmedAtUtc = null; pending.ConcurrencyStamp = Guid.NewGuid();
            user.LastVerificationSentAtUtc = Now;
            await email.QueueAsync(userId, new(user.Email, currentToken, AccountEmailPurpose.EmailChangeCurrent), cancellationToken);
            await email.QueueAsync(userId, new(target, newToken, AccountEmailPurpose.EmailChangeNew), cancellationToken);
            return user.Email;
        }, cancellationToken);
    }

    public async Task ConfirmAsync(string token, bool currentAddress, bool confirmed, CancellationToken cancellationToken)
    {
        if (!confirmed || !OpaqueToken.IsValid(token)) throw InvalidLink();
        var hash = OpaqueToken.Hash(token);
        var userId = await database.EmailChangeRequests.AsNoTracking()
            .Where(item => currentAddress ? item.CurrentTokenHash == hash : item.NewTokenHash == hash)
            .Select(item => (Guid?)item.UserId).SingleOrDefaultAsync(cancellationToken);
        if (!userId.HasValue) throw InvalidLink();
        await WriteAsync(userId.Value, null, null, async user =>
        {
            var pending = await database.EmailChangeRequests.SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
            if (pending is null || pending.CurrentEmail != user.Email || pending.ExpiresAtUtc <= Now ||
                (currentAddress ? pending.CurrentTokenHash != hash || pending.CurrentConfirmedAtUtc.HasValue
                    : pending.NewTokenHash != hash || pending.NewConfirmedAtUtc.HasValue)) throw InvalidLink();
            if (currentAddress) pending.CurrentConfirmedAtUtc = Now; else pending.NewConfirmedAtUtc = Now;
            pending.ConcurrencyStamp = Guid.NewGuid();
            return true;
        }, cancellationToken);
    }

    public Task CancelAsync(Guid userId, Guid sessionId, CancelEmailChangeRequest request, CancellationToken cancellationToken) =>
        WriteAsync(userId, sessionId, null, async user =>
        {
            if (!request.Confirmed) throw new DeviceOperationException(400, "Confirmá la cancelación.");
            var pending = await FindPendingAsync(user, request.RequestId, cancellationToken);
            database.EmailChangeRequests.Remove(pending);
            return true;
        }, cancellationToken);

    public async Task CompleteAsync(Guid userId, Guid sessionId, CompleteEmailChangeRequest request, CancellationToken cancellationToken)
    {
        if (!request.Confirmed || string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 128)
            throw new DeviceOperationException(400, "Confirmá el cambio y el cierre de sesiones web, e ingresá tu contraseña actual.");
        await WriteAsync(userId, sessionId, request.CurrentPassword, async user =>
        {
            var pending = await FindPendingAsync(user, request.RequestId, cancellationToken);
            if (pending.ExpiresAtUtc <= Now || !pending.CurrentConfirmedAtUtc.HasValue || !pending.NewConfirmedAtUtc.HasValue)
                throw new DeviceOperationException(400, "La solicitud venció o falta confirmar una dirección.");
            if (await database.Users.AnyAsync(item => item.Id != userId && item.Email == pending.NewEmail, cancellationToken))
                throw new DeviceOperationException(409, "No se puede completar el cambio a esa dirección.");
            var previous = user.Email;
            user.Email = pending.NewEmail; user.EmailVerified = true;
            var sessions = await database.UserSessions.Where(item => item.UserId == userId && item.RevokedAtUtc == null).ToListAsync(cancellationToken);
            foreach (var item in sessions) { item.RevokedAtUtc = Now; item.ConcurrencyStamp = Guid.NewGuid(); }
            var tokens = await database.AccountActionTokens.Where(item => item.UserId == userId && item.ConsumedAtUtc == null).ToListAsync(cancellationToken);
            foreach (var item in tokens) { item.ConsumedAtUtc = Now; item.ConcurrencyStamp = Guid.NewGuid(); }
            database.EmailChangeRequests.Remove(pending);
            SecurityActivityRecorder.Add(database, userId, SecurityActivityKind.EmailChanged, Now);
            await email.QueueAsync(userId, new(previous, "", AccountEmailPurpose.EmailChanged), cancellationToken);
            await email.QueueAsync(userId, new(user.Email, "", AccountEmailPurpose.EmailChanged), cancellationToken);
            return (Previous: previous, Current: user.Email);
        }, cancellationToken);
    }

    private async Task<EmailChangeRequest> FindPendingAsync(User user, Guid requestId, CancellationToken cancellationToken)
    {
        var pending = await database.EmailChangeRequests.SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        return pending is not null && pending.Id == requestId && pending.CurrentEmail == user.Email ? pending
            : throw new DeviceOperationException(409, "La solicitud cambió o ya no está disponible. Actualizá Mi cuenta.");
    }

    private async Task<T> WriteAsync<T>(Guid userId, Guid? sessionId, string? password,
        Func<User, Task<T>> operation, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await using var transaction = database.Database.IsRelational()
                ? await database.Database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
                var user = database.Database.IsRelational()
                    ? await database.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id_usuario = {userId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
                    : await database.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
                if (user is null) throw new DeviceOperationException(sessionId.HasValue ? 401 : 400, "La cuenta o el enlace ya no están disponibles.");
                if (sessionId.HasValue && !await database.UserSessions.AsNoTracking().AnyAsync(item => item.Id == sessionId &&
                    item.UserId == userId && item.RevokedAtUtc == null && item.ExpiresAtUtc > Now, cancellationToken))
                    throw new DeviceOperationException(401, "La sesión ya no está disponible.");
                if (!user.EmailVerified) throw new DeviceOperationException(403, "Verificá primero tu correo actual.");
                if (password is not null)
                {
                    if (password.Length is < 1 or > 128 || user.LockoutEndUtc > Now) throw Unconfirmed();
                    if (user.LockoutEndUtc.HasValue) { user.LockoutEndUtc = null; user.FailedLoginAttempts = 0; }
                    if (passwords.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
                    {
                        user.FailedLoginAttempts++;
                        if (user.FailedLoginAttempts >= settings.Value.MaxFailedLoginAttempts) user.LockoutEndUtc = Now.AddMinutes(settings.Value.LockoutMinutes);
                        user.ConcurrencyStamp = Guid.NewGuid(); await database.SaveChangesAsync(cancellationToken);
                        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                        throw Unconfirmed();
                    }
                    user.FailedLoginAttempts = 0; user.LockoutEndUtc = null;
                }
                var result = await operation(user);
                user.ConcurrencyStamp = Guid.NewGuid(); await database.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                database.ChangeTracker.Clear();
                throw new DeviceOperationException(409, "No se puede completar el cambio. Actualizá Mi cuenta y revisá la dirección.");
            }
            catch (Exception exception) when (Retryable(exception))
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                database.ChangeTracker.Clear();
            }
        }
        throw new DeviceOperationException(409, "Otra operación modificó la cuenta. Actualizá Mi cuenta antes de volver a intentar.");
    }

    private static DeviceOperationException InvalidLink() => new(400, "El enlace no es válido o ya venció. Revisá la solicitud desde Mi cuenta.");
    private static DeviceOperationException Unconfirmed() => new(403, "No pudimos confirmar tu contraseña. Revisala o esperá antes de intentar de nuevo.");
    private static bool Retryable(Exception exception) => exception is DbUpdateConcurrencyException ||
        exception is InvalidOperationException { InnerException: { } inner } && Retryable(inner) ||
        exception is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } ||
        exception is DbUpdateException { InnerException: PostgresException
            { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } };
}
