using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services;

public sealed record RetentionCounts(int Sessions, int AccountTokens, int PairingCodes, int DeviceCredentials, int EmailChanges = 0);
public sealed record RetentionCutoffs(DateTime Sessions, DateTime AccountTokens, DateTime PairingCodes, DateTime DeviceCredentials);
public sealed record RetentionReport(string Status, DateTime EvaluatedAtUtc, RetentionCutoffs CutoffsUtc,
    int BatchSize, RetentionCounts? Eligible, RetentionCounts Removed);

public sealed class RetentionMaintenanceService(AppDbContext database, IOptions<RetentionOptions> configured, TimeProvider clock)
{
    // Shared by all API instances and manual maintenance commands for this database.
    private const long LockKey = 0x534341495245544E;
    private static readonly RetentionCounts Empty = new(0, 0, 0, 0);

    public async Task<RetentionReport> RunAsync(bool apply, CancellationToken cancellationToken)
    {
        var options = configured.Value;
        if (!RetentionOptions.Valid(options)) throw new InvalidOperationException("La configuración de retención no es válida.");
        if (apply && !options.Enabled) throw new InvalidOperationException("La purga está desactivada.");
        var now = clock.GetUtcNow().UtcDateTime;
        var cutoffs = new RetentionCutoffs(now.AddDays(-options.ClosedSessionDays), now.AddDays(-options.AccountTokenDays),
            now.AddDays(-options.PairingCodeDays), now.AddDays(-options.DeviceCredentialDays));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var transaction = database.Database.IsRelational()
                ? await database.Database.BeginTransactionAsync(apply ? IsolationLevel.ReadCommitted : IsolationLevel.RepeatableRead, cancellationToken)
                : null;
            try
            {
                if (apply && transaction is not null && !await database.Database
                    .SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock({LockKey}) AS \"Value\"").SingleAsync(cancellationToken))
                    return new("busy", now, cutoffs, options.BatchSize, null, Empty);

                // Refresh hashes are retained for the entire live session, even when
                // consumed. Only deleting an old closed session cascades its refreshes.
                var sessions = database.UserSessions.Where(item => item.ExpiresAtUtc <= cutoffs.Sessions || item.RevokedAtUtc <= cutoffs.Sessions);
                var tokens = database.AccountActionTokens.Where(item => item.ExpiresAtUtc <= cutoffs.AccountTokens || item.ConsumedAtUtc <= cutoffs.AccountTokens);
                var emailChanges = database.EmailChangeRequests.Where(item => item.ExpiresAtUtc <= cutoffs.AccountTokens);
                var codes = database.DevicePairingCodes.Where(item => item.ExpiresAtUtc <= cutoffs.PairingCodes ||
                    item.ConsumedAtUtc <= cutoffs.PairingCodes || item.RevokedAtUtc <= cutoffs.PairingCodes);
                var credentials = database.DeviceCredentials.Where(item => item.ExpiresAtUtc <= cutoffs.DeviceCredentials || item.RevokedAtUtc <= cutoffs.DeviceCredentials);
                var eligible = new RetentionCounts(await sessions.CountAsync(cancellationToken), await tokens.CountAsync(cancellationToken),
                    await codes.CountAsync(cancellationToken), await credentials.CountAsync(cancellationToken), await emailChanges.CountAsync(cancellationToken));
                var removed = Empty;
                if (apply)
                {
                    var removedTokens = await RemoveBatchAsync(tokens, options.BatchSize, cancellationToken);
                    var removedCodes = await RemoveBatchAsync(codes, options.BatchSize, cancellationToken);
                    var removedCredentials = await RemoveBatchAsync(credentials, options.BatchSize, cancellationToken);
                    var removedSessions = await RemoveBatchAsync(sessions, options.BatchSize, cancellationToken);
                    var removedEmailChanges = await RemoveBatchAsync(emailChanges, options.BatchSize, cancellationToken);
                    removed = new(removedSessions, removedTokens, removedCodes, removedCredentials, removedEmailChanges);
                }
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return new(apply ? "applied" : "preview", now, cutoffs, options.BatchSize, eligible, removed);
            }
            catch (Exception exception) when (Retryable(exception))
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                database.ChangeTracker.Clear();
            }
        }
        throw new InvalidOperationException("Otra operación modificó los registros técnicos. Volvé a intentar el mantenimiento.");
    }

    private async Task<int> RemoveBatchAsync<TEntity>(IQueryable<TEntity> eligible, int limit, CancellationToken cancellationToken) where TEntity : class
    {
        var ids = await eligible.AsNoTracking().OrderBy(item => EF.Property<DateTime>(item, "ExpiresAtUtc"))
            .ThenBy(item => EF.Property<Guid>(item, "Id")).Take(limit).Select(item => EF.Property<Guid>(item, "Id")).ToArrayAsync(cancellationToken);
        // Recheck the predicate in the deletion statement instead of trusting old IDs.
        var batch = eligible.Where(item => ids.Contains(EF.Property<Guid>(item, "Id")));
        if (database.Database.IsRelational()) return await batch.ExecuteDeleteAsync(cancellationToken);
        // InMemory does not implement database cascades or ExecuteDelete.
        if (typeof(TEntity) == typeof(UserSession))
            await database.RefreshCredentials.Where(item => ids.Contains(item.UserSessionId)).LoadAsync(cancellationToken);
        var rows = await batch.AsTracking().ToListAsync(cancellationToken);
        database.RemoveRange(rows);
        await database.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    private static bool Retryable(Exception exception) =>
        exception is InvalidOperationException { InnerException: { } inner } && Retryable(inner) ||
        exception is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } ||
        exception is DbUpdateException { InnerException: PostgresException
            { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } };
}
