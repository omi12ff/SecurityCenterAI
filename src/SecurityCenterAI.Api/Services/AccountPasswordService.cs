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

public sealed class AccountPasswordService(AppDbContext database, IPasswordHasher<User> passwords,
    IOptions<AuthSecurityOptions> security, TimeProvider clock, IAccountEmailSender email)
{
    public async Task ChangeAsync(Guid userId, Guid sessionId, ChangeAccountPasswordRequest request, CancellationToken cancellationToken)
    {
        NewPasswordPolicy.Require(request.NewPassword);
        if (!request.Confirmed || string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 128 ||
            string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
            throw new DeviceOperationException(400, "Revisá la contraseña nueva y confirmá el cierre de sesiones.");
        string? recipient = null;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await using var transaction = database.Database.IsRelational()
                ? await database.Database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
                var user = database.Database.IsRelational()
                    ? await database.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id_usuario = {userId} FOR UPDATE")
                        .SingleOrDefaultAsync(cancellationToken)
                    : await database.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
                var now = clock.GetUtcNow().UtcDateTime;
                if (user is null || !await database.UserSessions.AsNoTracking().AnyAsync(item => item.Id == sessionId &&
                    item.UserId == userId && item.RevokedAtUtc == null && item.ExpiresAtUtc > now, cancellationToken))
                    throw new DeviceOperationException(401, "La sesión ya no está disponible.");
                if (user.LockoutEndUtc > now) throw Unconfirmed();
                if (user.LockoutEndUtc.HasValue) { user.LockoutEndUtc = null; user.FailedLoginAttempts = 0; }
                if (passwords.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
                {
                    user.FailedLoginAttempts++;
                    if (user.FailedLoginAttempts >= security.Value.MaxFailedLoginAttempts)
                        user.LockoutEndUtc = now.AddMinutes(security.Value.LockoutMinutes);
                    user.ConcurrencyStamp = Guid.NewGuid();
                    await database.SaveChangesAsync(cancellationToken);
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                    throw Unconfirmed();
                }
                user.PasswordHash = passwords.HashPassword(user, request.NewPassword);
                user.FailedLoginAttempts = 0;
                user.LockoutEndUtc = null;
                user.ConcurrencyStamp = Guid.NewGuid();
                var sessions = await database.UserSessions.Where(item => item.UserId == userId && item.RevokedAtUtc == null).ToListAsync(cancellationToken);
                foreach (var item in sessions) { item.RevokedAtUtc = now; item.ConcurrencyStamp = Guid.NewGuid(); }
                var recovery = await database.AccountActionTokens.Where(item => item.UserId == userId &&
                    item.Purpose == AccountService.PasswordResetPurpose && item.ConsumedAtUtc == null).ToListAsync(cancellationToken);
                foreach (var item in recovery) { item.ConsumedAtUtc = now; item.ConcurrencyStamp = Guid.NewGuid(); }
                SecurityActivityRecorder.Add(database, userId, SecurityActivityKind.PasswordChanged, now);
                var emailChange = await database.EmailChangeRequests.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
                if (emailChange is not null) database.EmailChangeRequests.Remove(emailChange);
                await email.QueueAsync(userId, new(user.Email, "", AccountEmailPurpose.PasswordChanged), cancellationToken);
                await database.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                recipient = user.Email;
                break;
            }
            catch (Exception exception) when (Retryable(exception))
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                database.ChangeTracker.Clear();
            }
        }
        if (recipient is null) throw new DeviceOperationException(409, "Otra operación modificó tu cuenta. Revisá su estado antes de volver a intentar.");
    }

    private static DeviceOperationException Unconfirmed() => new(403, "No pudimos confirmar tu contraseña. Revisala o esperá antes de intentar de nuevo.");
    private static bool Retryable(Exception exception) => exception is DbUpdateConcurrencyException ||
        exception is InvalidOperationException { InnerException: { } inner } && Retryable(inner) ||
        exception is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } ||
        exception is DbUpdateException { InnerException: PostgresException
            { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } };
}
