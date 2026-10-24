using System.Data;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Npgsql;

namespace SecurityCenterAI.Api.Services.Backups;

public sealed record BackupResult(string Status, string File, DateTime SnapshotAtUtc, int TablesVerified);
public sealed class BackupCleanupException(string database) : Exception("No se confirmó la creación o retirada de la base temporal.")
{
    public string Database { get; } = database;
}

public sealed class DatabaseBackupService
{
    private readonly string _repository;
    private readonly NpgsqlConnectionStringBuilder _connection;

    public DatabaseBackupService(string connectionString, string repository)
    {
        _repository = Path.GetFullPath(repository);
        _connection = new(connectionString) { Pooling = false };
        if (_connection.Host != "127.0.0.1" || _connection.Port != 5432 || string.IsNullOrWhiteSpace(_connection.Database) ||
            _connection.Database.StartsWith("scai_restore_test_", StringComparison.Ordinal) ||
            !System.IO.File.Exists(Path.Combine(_repository, "docker-compose.yml")))
            throw new InvalidOperationException("El mantenimiento admite únicamente PostgreSQL local de este proyecto.");
    }

    [SupportedOSPlatform("windows")]
    public async Task<BackupResult> CreateAsync(string directory, string password, CancellationToken cancellationToken)
    {
        BackupEnvelope.ValidatePassword(password);
        directory = BackupFiles.PrepareDirectory(directory, _repository);
        byte[]? dump = null, payload = null;
        string? partial = null;
        try
        {
            BackupManifest manifest;
            await using (var source = new NpgsqlConnection(_connection.ConnectionString))
            {
                await source.OpenAsync(cancellationToken);
                await RequirePostgres16Async(source, cancellationToken);
                await using var transaction = await source.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
                await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", source, transaction))
                    await readOnly.ExecuteNonQueryAsync(cancellationToken);
                string snapshot;
                await using (var export = new NpgsqlCommand("SELECT pg_export_snapshot()", source, transaction))
                    snapshot = (string)(await export.ExecuteScalarAsync(cancellationToken))!;
                var created = DateTime.UtcNow;
                var inventory = await BackupInventoryReader.ReadAsync(source, transaction, cancellationToken);
                dump = await DockerAsync("exec pg_dump --username=\"$POSTGRES_USER\" --dbname=\"$1\" --snapshot=\"$2\" --format=custom --no-owner --no-privileges --lock-wait-timeout=10s",
                    [_connection.Database!, snapshot], null, cancellationToken);
                manifest = new(1, 16, created, inventory, Convert.ToHexString(SHA256.HashData(dump)));
                await transaction.CommitAsync(cancellationToken);
            }
            payload = BackupPackage.Pack(manifest, dump);
            CryptographicOperations.ZeroMemory(dump); dump = null;
            var encrypted = BackupEnvelope.Encrypt(payload, password);
            CryptographicOperations.ZeroMemory(payload); payload = null;
            var file = Path.Combine(directory, $"SecurityCenterAI-{DateTime.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}.scaibackup");
            partial = file + ".partial";
            await using (var stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(encrypted, cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            // Verify the actual file, then publish the final name only on success.
            var verified = await VerifyAsync(partial, password, cancellationToken);
            System.IO.File.Move(partial, file); partial = null;
            return verified with { Status = "created-and-verified", File = file };
        }
        finally
        {
            if (dump is not null) CryptographicOperations.ZeroMemory(dump);
            if (payload is not null) CryptographicOperations.ZeroMemory(payload);
            if (partial is not null) System.IO.File.Delete(partial);
        }
    }

    public async Task<BackupResult> VerifyAsync(string file, string password, CancellationToken cancellationToken)
    {
        BackupEnvelope.ValidatePassword(password);
        byte[]? payload = null, dump = null;
        var temporary = "scai_restore_test_" + Guid.NewGuid().ToString("N");
        var created = false;
        try
        {
            // Authenticate and validate the entire artifact before creating any database.
            byte[] encrypted;
            await using (var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length > BackupEnvelope.MaxPlaintextBytes + BackupEnvelope.HeaderBytes + BackupEnvelope.TagBytes)
                    throw new InvalidDataException("Archivo de copia fuera de límite.");
                encrypted = new byte[(int)input.Length];
                await input.ReadExactlyAsync(encrypted, cancellationToken);
                if (input.ReadByte() != -1) throw new InvalidDataException("El archivo cambió durante la lectura.");
            }
            payload = BackupEnvelope.Decrypt(encrypted, password);
            var unpacked = BackupPackage.Unpack(payload);
            var manifest = unpacked.Manifest; dump = unpacked.Dump;
            var maintenance = new NpgsqlConnectionStringBuilder(_connection.ConnectionString) { Database = "postgres" };
            await using (var source = new NpgsqlConnection(maintenance.ConnectionString))
            { await source.OpenAsync(cancellationToken); await RequirePostgres16Async(source, cancellationToken); }
            // Finish this short DDL step independently of a user's cancellation,
            // so that a successful creation is always recorded for cleanup.
            try
            {
                using var creation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await DockerAsync("exec createdb --username=\"$POSTGRES_USER\" --template=template0 \"$1\"", [temporary], null, creation.Token);
                created = true;
            }
            catch { throw new BackupCleanupException(temporary); }
            var restoredConnection = new NpgsqlConnectionStringBuilder(_connection.ConnectionString) { Database = temporary };
            await using (var restored = new NpgsqlConnection(restoredConnection.ConnectionString))
            {
                await restored.OpenAsync(cancellationToken);
                await using var restrict = new NpgsqlCommand($"REVOKE CONNECT ON DATABASE \"{temporary}\" FROM PUBLIC", restored);
                await restrict.ExecuteNonQueryAsync(cancellationToken);
            }
            await DockerAsync("exec pg_restore --username=\"$POSTGRES_USER\" --dbname=\"$1\" --exit-on-error --single-transaction --no-owner --no-privileges", [temporary], dump, cancellationToken);
            await using (var restored = new NpgsqlConnection(restoredConnection.ConnectionString))
            {
                await restored.OpenAsync(cancellationToken);
                await using var transaction = await restored.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
                var actual = await BackupInventoryReader.ReadAsync(restored, transaction, cancellationToken);
                if (!BackupInventoryReader.Same(manifest.Inventory, actual))
                    throw new InvalidDataException($"La copia restaurada no coincide con su inventario. Esquema coincidente: {manifest.Inventory.SchemaSha256 == actual.SchemaSha256}; tablas distintas: " +
                        string.Join(",", manifest.Inventory.Tables.Except(actual.Tables).Select(table => table.Name)));
                await transaction.CommitAsync(cancellationToken);
            }
            return new("verified", Path.GetFullPath(file), manifest.CreatedAtUtc, manifest.Inventory.Tables.Length);
        }
        finally
        {
            if (dump is not null) CryptographicOperations.ZeroMemory(dump);
            if (payload is not null) CryptographicOperations.ZeroMemory(payload);
            if (created)
            {
                if (!Regex.IsMatch(temporary, "^scai_restore_test_[a-f0-9]{32}$")) throw new InvalidOperationException("Destino temporal rechazado.");
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try { await DockerAsync("exec dropdb --force --username=\"$POSTGRES_USER\" \"$1\"", [temporary], null, cleanup.Token); }
                catch { throw new BackupCleanupException(temporary); }
            }
        }
    }

    private static async Task RequirePostgres16Async(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SHOW server_version_num", connection);
        if (!int.TryParse((string?)await command.ExecuteScalarAsync(cancellationToken), out var version) || version / 10000 != 16)
            throw new InvalidOperationException("Este ensayo requiere PostgreSQL 16.");
    }

    private async Task<byte[]> DockerAsync(string script, string[] arguments, byte[]? input, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("docker") { WorkingDirectory = _repository, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "compose", "exec", "-T", "postgres", "sh", "-c", script, "scai-backup" }.Concat(arguments)) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("No se pudo iniciar Docker.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        using var output = new MemoryStream();
        try
        {
            async Task ReadOutputAsync()
            {
                var buffer = new byte[81920];
                try
                {
                    int read;
                    while ((read = await process.StandardOutput.BaseStream.ReadAsync(buffer, deadline.Token)) > 0)
                    {
                        if (output.Length + read > BackupEnvelope.MaxPlaintextBytes) throw new InvalidDataException("Copia fuera del límite local de 64 MiB.");
                        await output.WriteAsync(buffer.AsMemory(0, read), deadline.Token);
                    }
                }
                catch { deadline.Cancel(); throw; }
                finally { CryptographicOperations.ZeroMemory(buffer); }
            }
            async Task WriteInputAsync()
            {
                try { if (input is not null) await process.StandardInput.BaseStream.WriteAsync(input, deadline.Token); }
                catch { deadline.Cancel(); throw; }
                finally { process.StandardInput.Close(); }
            }
            await Task.WhenAll(ReadOutputAsync(), WriteInputAsync(),
                process.StandardError.BaseStream.CopyToAsync(Stream.Null, deadline.Token), process.WaitForExitAsync(deadline.Token));
            if (process.ExitCode != 0) throw new InvalidOperationException("Falló el comando PostgreSQL dentro de Docker. No se imprimen datos de la base.");
            return output.ToArray();
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); }
            if (output.TryGetBuffer(out var buffer)) CryptographicOperations.ZeroMemory(buffer.AsSpan());
        }
    }
}
