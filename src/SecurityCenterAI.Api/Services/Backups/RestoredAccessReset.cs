using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services.Backups;

public sealed record RestoredAccessCounts(long SessionsRemoved, long RefreshCredentialsRemoved,
    long AccountTokensRemoved, long PairingCodesRemoved, long DeviceCredentialsRemoved,
    long EmailChangesRemoved, long QueuedEmailsRemoved, long UserVersionsReset, long DeviceVersionsReset,
    long DevicesNewlyRevoked);
public sealed record RestoredAccessReport(DateTime PreparedAtUtc, RestoredAccessCounts Access,
    int ContentTablesChecked, bool ContentPreserved, bool AccessInvalidationChecked);

// This helper is deliberately restricted to disposable restore databases, never a live target.
public static class RestoredAccessReset
{
    public const string SupportedMigration = "20261025143028_DurableAccountEmailOutbox";
    private static readonly string[] AccessTables = ["user_sessions", "refresh_credentials", "account_action_tokens",
        "device_pairing_codes", "device_credentials", "email_change_requests", "account_email_outbox"];
    private static readonly string[] ContentTables = ["__EFMigrationsHistory", "security_analyses", "security_findings",
        "analysis_feedback", "security_rules", "deleted_analysis_reports", "link_message_usage", "security_activity"];

    public static bool IsIsolatedTarget(string? host, int port, string database, string expected) =>
        host == "127.0.0.1" && port == 5432 && database == expected &&
        Regex.IsMatch(expected, "^scai_restore_test_[a-f0-9]{32}$", RegexOptions.CultureInvariant);

    public static void RequireSupportedTables(BackupInventory inventory)
    {
        var expected = AccessTables.Concat(ContentTables).Concat(["users", "devices"]).ToHashSet(StringComparer.Ordinal);
        if (inventory.Tables.Length != expected.Count || !expected.SetEquals(inventory.Tables.Select(table => table.Name)))
            throw new InvalidDataException("El esquema de la copia requiere revisar la política de invalidación.");
    }

    public static async Task<RestoredAccessReport> PrepareAsync(NpgsqlConnection connection, string expectedDatabase,
        BackupInventory verifiedInventory, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open || !IsIsolatedTarget(connection.Host, connection.Port, connection.Database, expectedDatabase))
            throw new InvalidOperationException("La preparación solo admite el destino temporal propio del ensayo.");
        RequireSupportedTables(verifiedInventory);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        // All identifiers come from the closed policy. Lock only this disposable copy.
        var tables = verifiedInventory.Tables.Select(table => table.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var names = string.Join(", ", tables.Select(name => "public.\"" + name + "\""));
        await using (var guard = new NpgsqlCommand("SET LOCAL lock_timeout = '3000ms'; SET LOCAL statement_timeout = '30000ms'", connection, transaction))
            await guard.ExecuteNonQueryAsync(cancellationToken);
        await using (var locked = new NpgsqlCommand($"LOCK TABLE {names} IN ACCESS EXCLUSIVE MODE", connection, transaction))
            await locked.ExecuteNonQueryAsync(cancellationToken);
        await using (var target = new NpgsqlCommand("SELECT current_database()", connection, transaction))
            if ((string?)await target.ExecuteScalarAsync(cancellationToken) != expectedDatabase)
                throw new InvalidOperationException("El destino cambió durante el ensayo.");
        await using (var schema = new NpgsqlCommand("SELECT max(\"MigrationId\") FROM public.\"__EFMigrationsHistory\"", connection, transaction))
            if ((string?)await schema.ExecuteScalarAsync(cancellationToken) != SupportedMigration)
                throw new InvalidDataException("La versión de la copia requiere revisar la política de invalidación.");
        await RequireReviewedColumnsAsync(connection, transaction, cancellationToken);
        var before = await BackupInventoryReader.ReadAsync(connection, transaction, cancellationToken);
        if (!BackupInventoryReader.Same(verifiedInventory, before))
            throw new InvalidDataException("La copia cambió después de verificar su inventario.");
        var beforeProjection = await ReadContentProjectionAsync(connection, transaction, cancellationToken);
        var beforeRows = before.Tables.ToDictionary(table => table.Name, table => table.Rows, StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        async Task<long> Remove(string table)
        {
            await using var command = new NpgsqlCommand($"DELETE FROM public.\"{table}\"", connection, transaction);
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected != beforeRows[table]) throw new InvalidDataException("No se pudo invalidar todo el material de acceso de la copia.");
            return affected;
        }
        // Remove dependent refresh rows explicitly so counts remain meaningful before session cascade.
        var refresh = await Remove("refresh_credentials"); var sessions = await Remove("user_sessions");
        var accountTokens = await Remove("account_action_tokens"); var pairing = await Remove("device_pairing_codes");
        var credentials = await Remove("device_credentials"); var emailChanges = await Remove("email_change_requests");
        var queued = await Remove("account_email_outbox");
        long newlyRevoked;
        await using (var count = new NpgsqlCommand("SELECT count(*) FROM public.devices WHERE fecha_revocacion_utc IS NULL", connection, transaction))
            newlyRevoked = (long)(await count.ExecuteScalarAsync(cancellationToken))!;
        long users, devices;
        await using (var update = new NpgsqlCommand("UPDATE public.users SET version_concurrencia = gen_random_uuid()", connection, transaction))
            users = await update.ExecuteNonQueryAsync(cancellationToken);
        await using (var update = new NpgsqlCommand("UPDATE public.devices SET fecha_revocacion_utc = coalesce(fecha_revocacion_utc, @now), version_concurrencia = gen_random_uuid()", connection, transaction))
        { update.Parameters.AddWithValue("now", now); devices = await update.ExecuteNonQueryAsync(cancellationToken); }
        if (users != beforeRows["users"] || devices != beforeRows["devices"])
            throw new InvalidDataException("No se pudieron renovar las versiones de la copia completa.");
        var after = await BackupInventoryReader.ReadAsync(connection, transaction, cancellationToken);
        var afterRows = after.Tables.ToDictionary(table => table.Name, table => table.Rows, StringComparer.Ordinal);
        if (after.SchemaSha256 != before.SchemaSha256 || AccessTables.Any(table => afterRows[table] != 0) ||
            ContentTables.Any(table => !before.Tables.Single(item => item.Name == table).Equals(after.Tables.Single(item => item.Name == table))) ||
            !beforeProjection.SequenceEqual(await ReadContentProjectionAsync(connection, transaction, cancellationToken)))
            throw new InvalidDataException("No se pudo comprobar la conservación del contenido y la retirada de accesos.");
        await using (var active = new NpgsqlCommand("SELECT count(*) FROM public.devices WHERE fecha_revocacion_utc IS NULL", connection, transaction))
            if ((long)(await active.ExecuteScalarAsync(cancellationToken))! != 0)
                throw new InvalidDataException("Quedan dispositivos con acceso habilitado en la copia.");
        await transaction.CommitAsync(cancellationToken);
        return new(now, new(sessions, refresh, accountTokens, pairing, credentials, emailChanges, queued, users, devices, newlyRevoked),
            ContentTables.Length + 2, true, true);
    }

    private sealed record ReviewedColumn(string Table, string Name, string Type, bool Nullable);
    private static readonly Lazy<ReviewedColumn[]> ReviewedColumns = new(() =>
    {
        // Constructing the model performs no connection or migration. The migration guard above
        // still requires an explicit policy review before supporting a future application schema.
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=postgres").Options);
        return context.Model.GetRelationalModel().Tables.SelectMany(table => table.Columns
                .Select(column => new ReviewedColumn(table.Name, column.Name, column.StoreType, column.IsNullable)))
            .Concat([new("__EFMigrationsHistory", "MigrationId", "character varying(150)", false),
                new("__EFMigrationsHistory", "ProductVersion", "character varying(32)", false)])
            .OrderBy(column => column.Table, StringComparer.Ordinal).ThenBy(column => column.Name, StringComparer.Ordinal).ToArray();
    });
    private static async Task RequireReviewedColumnsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c.relname, a.attname, pg_catalog.format_type(a.atttypid, a.atttypmod), NOT a.attnotnull
            FROM pg_catalog.pg_attribute a
            JOIN pg_catalog.pg_class c ON c.oid = a.attrelid
            JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relkind = 'r' AND a.attnum > 0 AND NOT a.attisdropped
            """;
        var actual = new List<ReviewedColumn>();
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) actual.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3)));
        if (!ReviewedColumns.Value.SequenceEqual(actual.OrderBy(column => column.Table, StringComparer.Ordinal).ThenBy(column => column.Name, StringComparer.Ordinal)))
            throw new InvalidDataException("Las columnas de la copia requieren revisar la política de invalidación.");
    }

    private static async Task<BackupTable[]> ReadContentProjectionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var tables = new List<BackupTable>();
        foreach (var (name, projection) in new[] {
            ("users", "to_jsonb(t) - 'version_concurrencia'"),
            ("devices", "to_jsonb(t) - ARRAY['version_concurrencia','fecha_revocacion_utc']") })
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); long rows = 0;
            await using var command = new NpgsqlCommand($"SELECT ({projection})::text FROM public.\"{name}\" t ORDER BY ({projection})::text COLLATE \"C\"", connection, transaction);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var bytes = Encoding.UTF8.GetBytes(reader.GetString(0));
                try { hash.AppendData(bytes); hash.AppendData("\n"u8); }
                finally { CryptographicOperations.ZeroMemory(bytes); }
                rows++;
            }
            tables.Add(new(name, rows, Convert.ToHexString(hash.GetHashAndReset())));
        }
        return tables.ToArray();
    }
}
