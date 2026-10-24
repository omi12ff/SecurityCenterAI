using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace SecurityCenterAI.Api.Services.Backups;

public sealed record BackupTable(string Name, long Rows, string Sha256);
public sealed record BackupInventory(string SchemaSha256, BackupTable[] Tables, string[]? SchemaDefinitions = null);

public static class BackupInventoryReader
{
    public static async Task<BackupInventory> ReadAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        // Stabilize JSON formatting for both source and isolated restore.
        await using (var settings = new NpgsqlCommand("SET LOCAL TIME ZONE 'UTC'; SET LOCAL datestyle = 'ISO, YMD'; SET LOCAL extra_float_digits = 3", connection, transaction))
            await settings.ExecuteNonQueryAsync(cancellationToken);
        var names = new List<string>();
        await using (var command = new NpgsqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE' ORDER BY table_name COLLATE \"C\"", connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) names.Add(reader.GetString(0));
        if (!names.Contains("users") || !names.Contains("__EFMigrationsHistory"))
            throw new InvalidDataException("No se reconoce el esquema migrado de Security Center AI.");
        var tables = new List<BackupTable>();
        foreach (var name in names)
        {
            var identifier = '"' + name.Replace("\"", "\"\"") + '"';
            var sql = $"SELECT to_jsonb(t)::text FROM public.{identifier} t ORDER BY to_jsonb(t)::text COLLATE \"C\"";
            var (rows, hash) = await HashQueryAsync(connection, transaction, sql, cancellationToken);
            tables.Add(new(name, rows, hash));
        }
        const string schema = """
            SELECT definition FROM (
              SELECT 'column:' || row_to_json(c)::text AS definition FROM (
                SELECT table_name,column_name,ordinal_position,column_default,is_nullable,data_type,
                       udt_name,character_maximum_length,numeric_precision,numeric_scale
                FROM information_schema.columns WHERE table_schema='public'
              ) c
              UNION ALL SELECT 'index:' || indexdef FROM pg_indexes WHERE schemaname='public'
              UNION ALL SELECT 'constraint:' || t.relname || ':' || c.conname || ':' || pg_get_constraintdef(c.oid,true)
                FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid
                JOIN pg_namespace n ON n.oid=t.relnamespace WHERE n.nspname='public' AND c.contype <> 'c'
            ) definitions ORDER BY definition COLLATE "C"
            """;
        var definitions = new List<string>();
        await HashQueryAsync(connection, transaction, schema, cancellationToken, definitions);
        // pg_dump/reparse can move a cast from a constant array to its elements.
        // PostgreSQL's planner folds both forms identically. Compare its output,
        // rather than erasing casts or ignoring CHECK constraints.
        var checks = new List<(string Table, string Name, string Expression, bool Validated, bool NoInherit)>();
        const string checkSql = """
            SELECT t.relname,c.conname,pg_get_expr(c.conbin,c.conrelid,true),c.convalidated,c.connoinherit
            FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid
            JOIN pg_namespace n ON n.oid=t.relnamespace WHERE n.nspname='public' AND c.contype='c'
            """;
        await using (var command = new NpgsqlCommand(checkSql, connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                checks.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3), reader.GetBoolean(4)));
        foreach (var check in checks)
        {
            var identifier = '"' + check.Table.Replace("\"", "\"\"") + '"';
            await using var explain = new NpgsqlCommand($"EXPLAIN (VERBOSE, FORMAT JSON, COSTS FALSE) SELECT ({check.Expression}) FROM public.{identifier}", connection, transaction);
            using var plan = JsonDocument.Parse((string)(await explain.ExecuteScalarAsync(cancellationToken))!);
            var expression = plan.RootElement[0].GetProperty("Plan").GetProperty("Output")[0].GetString();
            definitions.Add($"check:{check.Table}:{check.Name}:{check.Validated}:{check.NoInherit}:{expression}");
        }
        definitions.Sort(StringComparer.Ordinal);
        using var schemaDigest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var definition in definitions) schemaDigest.AppendData(Encoding.UTF8.GetBytes(definition + "\n"));
        var schemaHash = Convert.ToHexString(schemaDigest.GetHashAndReset());
        return new(schemaHash, tables.ToArray(), definitions.ToArray());
    }

    private static async Task<(long Rows, string Hash)> HashQueryAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken, List<string>? definitions = null)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var command = new NpgsqlCommand(sql, connection, transaction) { CommandTimeout = 120 };
        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, cancellationToken);
        long rows = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            var text = reader.GetString(0);
            definitions?.Add(text);
            var bytes = Encoding.UTF8.GetBytes(text);
            try { hash.AppendData(bytes); hash.AppendData("\n"u8); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
            rows++;
        }
        return (rows, Convert.ToHexString(hash.GetHashAndReset()));
    }

    public static bool Same(BackupInventory expected, BackupInventory actual) =>
        expected.SchemaSha256 == actual.SchemaSha256 && expected.Tables.SequenceEqual(actual.Tables);
}
