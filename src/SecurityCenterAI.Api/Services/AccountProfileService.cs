using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services;

public sealed class AccountProfileService(AppDbContext database, TimeProvider clock)
{
    public async Task UpdateNameAsync(Guid userId, Guid sessionId, string? submittedName, CancellationToken cancellationToken)
    {
        var name = submittedName?.Trim();
        if (name is null || submittedName!.Length > 200 || name.Length is < 2 or > 100 || name.Any(char.IsControl))
            throw new DeviceOperationException(400, "Usá un nombre de entre 2 y 100 caracteres, sin caracteres de control.");
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
                if (!string.Equals(user.Name, name, StringComparison.Ordinal))
                {
                    // Only these two fields change. Do not copy profile properties from the request.
                    user.Name = name;
                    user.ConcurrencyStamp = Guid.NewGuid();
                    await database.SaveChangesAsync(cancellationToken);
                }
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (Retryable(exception))
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                database.ChangeTracker.Clear();
            }
        }
        throw new DeviceOperationException(409, "Otra operación modificó tu cuenta. Actualizá el perfil y volvé a intentar.");
    }

    private static bool Retryable(Exception exception) => exception is DbUpdateConcurrencyException ||
        exception is InvalidOperationException { InnerException: { } inner } && Retryable(inner) ||
        exception is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } ||
        exception is DbUpdateException { InnerException: PostgresException
            { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } };
}
