using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecurityCenterAI.Api.Services.Backups;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class BackupPostgresFactAttribute : FactAttribute
{
    public BackupPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SECURITY_CENTER_BACKUP_TEST_CONNECTION")))
            Skip = "Run scripts/test-backup.ps1 for a fictitious isolated PostgreSQL database.";
    }
}

[Collection("BackupPostgres")]
public sealed class BackupPostgresTests
{
    private const string Password = "contraseña ficticia 🔐 solo pruebas 2026";
    private static string Connection()
    {
        var value = Environment.GetEnvironmentVariable("SECURITY_CENTER_BACKUP_TEST_CONNECTION") ?? throw new InvalidOperationException("Missing isolated connection.");
        var parsed = new NpgsqlConnectionStringBuilder(value) { Pooling = false };
        if (!Regex.IsMatch(parsed.Database ?? "", "^scai_backup_fixture_[a-f0-9]{32}$") || parsed.Host != "127.0.0.1" || parsed.Port != 5432)
            throw new InvalidOperationException("Refused a database outside the backup fixture namespace.");
        return parsed.ConnectionString;
    }
    private static string Repository()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            if (System.IO.File.Exists(Path.Combine(folder.FullName, "docker-compose.yml"))) return folder.FullName;
        throw new InvalidOperationException("Repository not found.");
    }
    private static string TestDirectory() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecurityCenterAI", "Backups", "test_" + Guid.NewGuid().ToString("N"));
    private static string Hash() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Connection()).Options);
    private static async Task<BackupInventory> InventoryAsync()
    {
        await using var connection = new NpgsqlConnection(Connection()); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        return await BackupInventoryReader.ReadAsync(connection, transaction, default);
    }
    private static async Task<int> RestoreDatabasesAsync()
    {
        await using var connection = new NpgsqlConnection(Connection()); await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*)::int FROM pg_database WHERE datname ~ '^scai_restore_test_[a-f0-9]{32}$'", connection);
        return (int)(await command.ExecuteScalarAsync())!;
    }
    private static void RemoveTestFiles(string directory)
    {
        var store = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecurityCenterAI", "Backups"));
        if (Path.GetDirectoryName(Path.GetFullPath(directory)) != store || !Regex.IsMatch(Path.GetFileName(directory), "^test_[a-f0-9]{32}$"))
            throw new InvalidOperationException("Refused test folder cleanup.");
        if (!Directory.Exists(directory)) return;
        foreach (var file in Directory.GetFiles(directory)) System.IO.File.Delete(file);
        Directory.Delete(directory, recursive: false);
    }

    [BackupPostgresFact]
    public async Task FullEncryptedRoundTripAndFailuresPreserveTheSourceAndRemoveTemporaryDatabases()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var db = Database(); await db.Database.MigrateAsync();
        var now = DateTime.UtcNow;
        var user = new User { Name = "Fixture de recuperación ñ", Email = $"backup-{Guid.NewGuid():N}@example.invalid", PasswordHash = "PRIVATE_FIXTURE_PASSWORD_HASH" };
        var device = new Device { User = user, Name = "Equipo ficticio", CreatedAtUtc = now };
        var analysis = new SecurityAnalysis { User = user, Device = device, ReportId = Guid.NewGuid(), Score = 42, AnalyzedAtUtc = now,
            Findings = [new SecurityFinding { RuleId = "WIN-TPM-READY", RuleVersion = 1, Title = "Ficticio", Severity = "informational", Evidence = "Evidencia ficticia ñ", Recommendation = "Revisar", DetectedAtUtc = now }] };
        db.SecurityAnalyses.Add(analysis);
        db.SecurityActivities.Add(new SecurityActivity { User = user, UserId = user.Id, Kind = SecurityActivityKind.AccountRegistered, OccurredAtUtc = now });
        db.SecurityActivities.Add(new SecurityActivity { User = user, UserId = user.Id, Kind = SecurityActivityKind.PasswordChanged, OccurredAtUtc = now });
        db.SecurityActivities.Add(new SecurityActivity { User = user, UserId = user.Id, Kind = SecurityActivityKind.EmailChanged, OccurredAtUtc = now });
        db.EmailChangeRequests.Add(new() { User = user, UserId = user.Id, CurrentEmail = user.Email,
            NewEmail = $"pending-{Guid.NewGuid():N}@example.invalid", CurrentTokenHash = new string('A', 64), NewTokenHash = new string('B', 64),
            CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(30) });
        db.LinkMessageUsage.Add(new() { User = user, UserId = user.Id, DayUtc = DateOnly.FromDateTime(now), Checks = 3 });
        db.AnalysisFeedback.Add(new AnalysisFeedback { User = user, Analysis = analysis, Rating = "helpful", Comment = "Comentario ficticio", CreatedAtUtc = now, UpdatedAtUtc = now, ExplanationVersion = 1 });
        db.DeletedAnalysisReports.Add(new DeletedAnalysisReport { Device = device, ReportId = Guid.NewGuid(), DeletedAtUtc = now });
        var session = new UserSession { User = user, CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(1) };
        db.UserSessions.Add(session);
        db.RefreshCredentials.Add(new RefreshCredential { Session = session, TokenHash = Hash(), CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(1), ConsumedAtUtc = now });
        db.AccountActionTokens.Add(new AccountActionToken { User = user, Purpose = "password-reset", TokenHash = Hash(), CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(30) });
        db.DevicePairingCodes.Add(new DevicePairingCode { Device = device, CodeHash = Hash(), CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(10) });
        db.DeviceCredentials.Add(new DeviceCredential { Device = device, TokenHash = Hash(), CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(1) });
        await db.SaveChangesAsync();
        var before = await InventoryAsync(); var temporaryBefore = await RestoreDatabasesAsync();
        var directory = TestDirectory(); var service = new DatabaseBackupService(Connection(), Repository());
        try
        {
            var created = await service.CreateAsync(directory, Password, default);
            Assert.Equal("created-and-verified", created.Status); Assert.Equal(before.Tables.Length, created.TablesVerified);
            Assert.True(BackupInventoryReader.Same(before, await InventoryAsync()));
            Assert.Equal(temporaryBefore, await RestoreDatabasesAsync());
            var bytes = await System.IO.File.ReadAllBytesAsync(created.File);
            Assert.DoesNotContain(user.Email, Encoding.UTF8.GetString(bytes));
            Assert.DoesNotContain(user.PasswordHash, Encoding.UTF8.GetString(bytes));
            Assert.Equal("verified", (await service.VerifyAsync(created.File, Password, default)).Status);
            var missingSource = new NpgsqlConnectionStringBuilder(Connection()) { Database = "scai_backup_fixture_" + Guid.NewGuid().ToString("N") };
            var recovery = new DatabaseBackupService(missingSource.ConnectionString, Repository());
            Assert.Equal("verified", (await recovery.VerifyAsync(created.File, Password, default)).Status);
            await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => service.VerifyAsync(created.File, "otra contraseña ficticia incorrecta", default));
            var corrupt = Path.Combine(directory, "altered.scaibackup"); bytes[^1] ^= 1;
            await System.IO.File.WriteAllBytesAsync(corrupt, bytes);
            await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => service.VerifyAsync(corrupt, Password, default));
            Assert.Equal(temporaryBefore, await RestoreDatabasesAsync());
            Assert.True(BackupInventoryReader.Same(before, await InventoryAsync()));

            // A correctly authenticated but inconsistent inventory must fail after
            // restore and still clean up, rather than reporting a false success.
            var decrypted = BackupEnvelope.Decrypt(await System.IO.File.ReadAllBytesAsync(created.File), Password);
            var unpacked = BackupPackage.Unpack(decrypted);
            var inconsistent = unpacked.Manifest with { Inventory = unpacked.Manifest.Inventory with { SchemaSha256 = new string('0', 64) } };
            var invalid = Path.Combine(directory, "inconsistent.scaibackup");
            await System.IO.File.WriteAllBytesAsync(invalid, BackupEnvelope.Encrypt(BackupPackage.Pack(inconsistent, unpacked.Dump), Password));
            CryptographicOperations.ZeroMemory(decrypted); CryptographicOperations.ZeroMemory(unpacked.Dump);
            await Assert.ThrowsAsync<InvalidDataException>(() => service.VerifyAsync(invalid, Password, default));
            Assert.Equal(temporaryBefore, await RestoreDatabasesAsync());
            Assert.True(BackupInventoryReader.Same(before, await InventoryAsync()));

            db.SecurityAnalyses.Remove(analysis); await db.SaveChangesAsync();
            var afterDeletion = await InventoryAsync();
            Assert.Equal("verified", (await service.VerifyAsync(created.File, Password, default)).Status);
            Assert.True(BackupInventoryReader.Same(afterDeletion, await InventoryAsync()));
            Assert.False(await db.SecurityAnalyses.AnyAsync(item => item.Id == analysis.Id));
            Assert.Equal(temporaryBefore, await RestoreDatabasesAsync());
        }
        finally { RemoveTestFiles(directory); }
    }

    [BackupPostgresFact]
    public async Task SchemaFingerprintDetectsAChangedCheckWithoutChangingAnyRows()
    {
        await using var db = Database(); await db.Database.MigrateAsync();
        var before = await InventoryAsync();
        await using var connection = new NpgsqlConnection(Connection()); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var change = new NpgsqlCommand("""
            ALTER TABLE security_findings DROP CONSTRAINT "CK_security_findings_severidad";
            ALTER TABLE security_findings ADD CONSTRAINT "CK_security_findings_severidad" CHECK (severidad <> 'different-forbidden-value');
            """, connection, transaction);
        await change.ExecuteNonQueryAsync();
        var changed = await BackupInventoryReader.ReadAsync(connection, transaction, default);
        Assert.Equal(before.Tables, changed.Tables);
        Assert.NotEqual(before.SchemaSha256, changed.SchemaSha256);
        await transaction.RollbackAsync();
        Assert.True(BackupInventoryReader.Same(before, await InventoryAsync()));
    }

    [BackupPostgresFact]
    public async Task ConcurrentWritesDoNotSeparateTheDumpFromItsSnapshotInventory()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var db = Database(); await db.Database.MigrateAsync();
        var user = new User { Name = "Concurrent fixture", Email = $"concurrent-{Guid.NewGuid():N}@example.invalid", PasswordHash = "FICTITIOUS_HASH" };
        db.Users.Add(user); await db.SaveChangesAsync();
        var directory = TestDirectory(); var service = new DatabaseBackupService(Connection(), Repository());
        using var stop = new CancellationTokenSource();
        var writes = 0;
        var writer = Task.Run(async () =>
        {
            await using var connection = new NpgsqlConnection(Connection()); await connection.OpenAsync();
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    await using var command = new NpgsqlCommand("UPDATE users SET nombre_completo=@name WHERE id_usuario=@id", connection);
                    command.Parameters.AddWithValue("name", "Fixture write " + writes); command.Parameters.AddWithValue("id", user.Id);
                    await command.ExecuteNonQueryAsync(stop.Token); Interlocked.Increment(ref writes);
                    await Task.Delay(10, stop.Token);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        });
        try
        {
            var result = await service.CreateAsync(directory, Password, default);
            Assert.Equal("created-and-verified", result.Status);
            Assert.True(writes > 1);
        }
        finally { stop.Cancel(); await writer; RemoveTestFiles(directory); }
    }
}
