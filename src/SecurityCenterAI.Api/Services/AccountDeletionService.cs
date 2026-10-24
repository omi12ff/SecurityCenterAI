using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services;

public sealed class AccountDeletionService(AppDbContext database, IPasswordHasher<User> passwords,
    IOptions<AuthSecurityOptions> security, TimeProvider clock)
{
    private static DeviceOperationException Expired() => new(401, "La sesión ya no está disponible.");
    private static DeviceOperationException Unconfirmed() => new(403, "No pudimos confirmar tu contraseña. Revisala o esperá antes de intentar de nuevo.");

    public async Task DeleteAsync(Guid userId, Guid sessionId, DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        if (request.Confirmation != "ELIMINAR" || string.IsNullOrEmpty(request.Password) || request.Password.Length > 128)
            throw new DeviceOperationException(400, "Escribí ELIMINAR e ingresá tu contraseña actual.");
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await using var transaction = database.Database.IsRelational()
                ? await database.Database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
                // Lock the owner before checking the current password. Reset, login,
                // refresh and ingestion coordinate through this same user row/version.
                var user = database.Database.IsRelational()
                    ? await database.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id_usuario = {userId} FOR UPDATE")
                        .SingleOrDefaultAsync(cancellationToken)
                    : await database.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
                var now = clock.GetUtcNow().UtcDateTime;
                if (user is null || !await database.UserSessions.AsNoTracking().AnyAsync(item => item.Id == sessionId &&
                    item.UserId == userId && item.RevokedAtUtc == null && item.ExpiresAtUtc > now, cancellationToken)) throw Expired();
                if (user.LockoutEndUtc > now) throw Unconfirmed();
                if (user.LockoutEndUtc.HasValue) { user.LockoutEndUtc = null; user.FailedLoginAttempts = 0; }
                if (passwords.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
                {
                    user.FailedLoginAttempts++;
                    if (user.FailedLoginAttempts >= security.Value.MaxFailedLoginAttempts)
                        user.LockoutEndUtc = now.AddMinutes(security.Value.LockoutMinutes);
                    user.ConcurrencyStamp = Guid.NewGuid();
                    await database.SaveChangesAsync(cancellationToken);
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                    throw Unconfirmed();
                }
                // PostgreSQL performs all cascades atomically from this single owner
                // deletion. InMemory has no server cascades, so track the graph there.
                if (!database.Database.IsRelational()) await LoadCascadeGraphAsync(userId, cancellationToken);
                database.Users.Remove(user);
                await database.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (Retryable(exception))
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                database.ChangeTracker.Clear();
            }
        }
        throw new DeviceOperationException(409, "Otra operación modificó tu cuenta. Revisá su estado antes de volver a intentar.");
    }

    private async Task LoadCascadeGraphAsync(Guid userId, CancellationToken cancellationToken)
    {
        await database.SecurityActivities.Where(item => item.UserId == userId).LoadAsync(cancellationToken);
        await database.UserSessions.Where(item => item.UserId == userId).LoadAsync(cancellationToken);
        await database.RefreshCredentials.Where(item => item.Session.UserId == userId).LoadAsync(cancellationToken);
        await database.AccountActionTokens.Where(item => item.UserId == userId).LoadAsync(cancellationToken);
        await database.EmailChangeRequests.Where(item => item.UserId == userId).LoadAsync(cancellationToken);
        await database.LinkMessageUsage.Where(item => item.UserId == userId).LoadAsync(cancellationToken);
        await database.Devices.Where(item => item.UserId == userId).LoadAsync(cancellationToken);
        await database.DeviceCredentials.Where(item => item.Device.UserId == userId).LoadAsync(cancellationToken);
        await database.DevicePairingCodes.Where(item => item.Device.UserId == userId).LoadAsync(cancellationToken);
        await database.DeletedAnalysisReports.Where(item => item.Device.UserId == userId).LoadAsync(cancellationToken);
        await database.SecurityAnalyses.Where(item => item.UserId == userId).Include(item => item.Findings).LoadAsync(cancellationToken);
        await database.AnalysisFeedback.Where(item => item.UserId == userId || item.Analysis.UserId == userId).LoadAsync(cancellationToken);
    }

    private static bool Retryable(Exception exception) => exception is DbUpdateConcurrencyException ||
        exception is InvalidOperationException { InnerException: { } inner } && Retryable(inner) ||
        RetryablePostgres(exception as PostgresException) ||
        exception is DbUpdateException { InnerException: PostgresException postgres } && RetryablePostgres(postgres);
    private static bool RetryablePostgres(PostgresException? exception) => exception?.SqlState is
        PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure;
}
