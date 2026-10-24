using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services;

public sealed class LinkMessageService(AppDbContext database, IOptions<LinkMessageOptions> options, TimeProvider clock)
{
    private LinkMessageQuota Quota(DateTime now, LinkMessageUsage? usage)
    {
        var day = DateOnly.FromDateTime(now);
        var used = usage?.DayUtc == day ? usage.Checks : 0;
        return new(day, used, options.Value.DailyLimit, Math.Max(0, options.Value.DailyLimit - used), now.Date.AddDays(1));
    }

    public async Task<LinkMessageQuota> StatusAsync(Guid ownerId, CancellationToken cancellationToken) =>
        Quota(clock.GetUtcNow().UtcDateTime, await database.LinkMessageUsage.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == ownerId, cancellationToken));

    public async Task<LinkMessageResponse> CheckAsync(Guid ownerId, Guid sessionId, LinkMessageRequest request, CancellationToken cancellationToken)
    {
        var report = LocalScamRules.Check(request);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await using var transaction = database.Database.IsRelational()
                ? await database.Database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
                var user = database.Database.IsRelational()
                    ? await database.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id_usuario = {ownerId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
                    : await database.Users.SingleOrDefaultAsync(item => item.Id == ownerId, cancellationToken);
                var now = clock.GetUtcNow().UtcDateTime;
                if (user is null || !await database.UserSessions.AsNoTracking().AnyAsync(item => item.Id == sessionId && item.UserId == ownerId &&
                    item.RevokedAtUtc == null && item.ExpiresAtUtc > now, cancellationToken)) throw new DeviceOperationException(401, "La sesión ya no está disponible.");
                if (!user.EmailVerified) throw new DeviceOperationException(403, "Verificá tu correo desde Mi cuenta antes de usar esta herramienta.");
                var usage = await database.LinkMessageUsage.SingleOrDefaultAsync(item => item.UserId == ownerId, cancellationToken);
                var current = Quota(now, usage);
                if (current.Remaining == 0) throw new DeviceOperationException(429, "Alcanzaste el límite diario de enlaces y mensajes. Se renueva al comenzar el siguiente día UTC.");
                if (usage is null) { usage = new() { UserId = ownerId }; database.LinkMessageUsage.Add(usage); }
                usage.DayUtc = current.DayUtc; usage.Checks = current.Used + 1; usage.ConcurrencyStamp = Guid.NewGuid();
                user.ConcurrencyStamp = Guid.NewGuid();
                await database.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return new(report, Quota(now, usage), now);
            }
            catch (Exception exception) when (Retryable(exception))
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                database.ChangeTracker.Clear();
            }
        }
        throw new DeviceOperationException(409, "Otra operación modificó la cuenta. Volvé a intentar.");
    }

    private static bool Retryable(Exception exception) => exception is DbUpdateConcurrencyException ||
        exception is InvalidOperationException { InnerException: { } inner } && Retryable(inner) ||
        exception is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } ||
        exception is DbUpdateException { InnerException: PostgresException
            { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } };
}
