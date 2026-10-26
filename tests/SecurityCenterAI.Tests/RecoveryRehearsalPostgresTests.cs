using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Api.Services.Backups;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

[CollectionDefinition("BackupPostgres", DisableParallelization = true)]
public sealed class BackupPostgresCollection;

[Collection("BackupPostgres")]
public sealed class RecoveryRehearsalPostgresTests
{
    private const string Private = "PRIVATE_RECOVERY_FIXTURE_ONLY";
    private const string Password = "contraseña de ensayo ficticio 🔐 2026";
    private static string SourceConnection()
    {
        var value = Environment.GetEnvironmentVariable("SECURITY_CENTER_BACKUP_TEST_CONNECTION") ?? throw new InvalidOperationException("Missing isolated fixture connection.");
        var parsed = new NpgsqlConnectionStringBuilder(value) { Pooling = false };
        if (parsed.Host != "127.0.0.1" || parsed.Port != 5432 || !Regex.IsMatch(parsed.Database ?? "", "^scai_backup_fixture_[a-f0-9]{32}$"))
            throw new InvalidOperationException("Unsafe recovery fixture source.");
        return parsed.ConnectionString;
    }
    private static string Repository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "SecurityCenterAI.sln"))) return directory.FullName;
        throw new InvalidOperationException("Repository not found.");
    }
    private static AppDbContext Database(string connection) => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    private static async Task<BackupInventory> InventoryAsync(NpgsqlConnection connection)
    {
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        var inventory = await BackupInventoryReader.ReadAsync(connection, transaction, default);
        await transaction.CommitAsync(); return inventory;
    }
    private static async Task<int> TemporaryCountAsync(NpgsqlConnection connection)
    { await using var command = new NpgsqlCommand("SELECT count(*)::int FROM pg_database WHERE datname ~ '^scai_restore_test_[a-f0-9]{32}$'", connection); return (int)(await command.ExecuteScalarAsync())!; }

    private static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync(); var now = DateTime.UtcNow;
        var user = new User { Name = "Contenido de recuperación ñ", Email = $"recovery-{Guid.NewGuid():N}@example.invalid", PasswordHash = Private, EmailVerified = true };
        var device = new Device { User = user, Name = "Equipo de ensayo", CreatedAtUtc = now, PairedAtUtc = now, Platform = "Windows", AgentVersion = "fictitious" };
        var analysis = new SecurityAnalysis { User = user, Device = device, ReportId = Guid.NewGuid(), Score = 42, AnalyzedAtUtc = now,
            Findings = [new() { RuleId = "WIN-TPM-READY", RuleVersion = 1, Title = "Ficticio", Severity = "informational", Evidence = Private,
                Recommendation = "Conservar contenido", DetectedAtUtc = now }] };
        db.SecurityAnalyses.Add(analysis);
        db.AnalysisFeedback.Add(new() { User = user, Analysis = analysis, Rating = "helpful", Comment = Private,
            ExplanationVersion = 1, CreatedAtUtc = now, UpdatedAtUtc = now });
        db.DeletedAnalysisReports.Add(new() { Device = device, ReportId = Guid.NewGuid(), DeletedAtUtc = now });
        db.SecurityActivities.Add(new() { User = user, Kind = SecurityActivityKind.AccountRegistered, OccurredAtUtc = now });
        db.LinkMessageUsage.Add(new() { User = user, DayUtc = DateOnly.FromDateTime(now), Checks = 2 });
        var session = new UserSession { User = user, CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(1) }; db.UserSessions.Add(session);
        db.RefreshCredentials.Add(new() { Session = session, TokenHash = Hash(), CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(1) });
        db.AccountActionTokens.Add(new() { User = user, Purpose = "password-reset", TokenHash = Hash(), CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(30) });
        db.DevicePairingCodes.Add(new() { Device = device, CodeHash = Hash(), CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(10) });
        db.DeviceCredentials.Add(new() { Device = device, TokenHash = Hash(), CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(1) });
        db.EmailChangeRequests.Add(new() { User = user, CurrentEmail = user.Email, NewEmail = $"next-{Guid.NewGuid():N}@example.invalid",
            CurrentTokenHash = Hash(), NewTokenHash = Hash(), CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(15) });
        db.AccountEmailOutbox.Add(new() { UserId = user.Id, CreatedAtUtc = now, ExpiresAtUtc = now.AddHours(1), NextAttemptAtUtc = now, ProtectedPayload = Private });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    }
    private static string Hash() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    [BackupPostgresFact]
    public async Task EncryptedRehearsalPreservesTheSourceAndFileAndRemovesItsTemporaryTargetOnEveryOutcome()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var db = Database(SourceConnection()); await SeedAsync(db);
        await using var connection = new NpgsqlConnection(SourceConnection()); await connection.OpenAsync();
        var before = await InventoryAsync(connection); var temporaryBefore = await TemporaryCountAsync(connection);
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecurityCenterAI", "Backups", "test_" + Guid.NewGuid().ToString("N"));
        var service = new DatabaseBackupService(SourceConnection(), Repository());
        try
        {
            var backup = await service.CreateAsync(directory, Password, default);
            var encrypted = await File.ReadAllBytesAsync(backup.File); var originalDigest = SHA256.HashData(encrypted);
            var report = await service.RehearseRecoveryAsync(backup.File, Password, default);
            Assert.Equal("rehearsed-in-isolation", report.Status); Assert.True(report.Preparation.ContentPreserved); Assert.True(report.Preparation.AccessInvalidationChecked);
            Assert.False(report.ActiveDatabaseReplaced); Assert.False(report.LaterDeletionsReconciled); Assert.Equal(10, report.Preparation.ContentTablesChecked);
            var counts = before.Tables.ToDictionary(table => table.Name, table => table.Rows);
            Assert.Equal(counts["user_sessions"], report.Preparation.Access.SessionsRemoved);
            Assert.Equal(counts["refresh_credentials"], report.Preparation.Access.RefreshCredentialsRemoved);
            Assert.Equal(counts["account_action_tokens"], report.Preparation.Access.AccountTokensRemoved);
            Assert.Equal(counts["account_email_outbox"], report.Preparation.Access.QueuedEmailsRemoved);
            Assert.DoesNotContain(Private, JsonSerializer.Serialize(report));
            Assert.True(BackupInventoryReader.Same(before, await InventoryAsync(connection)));
            Assert.Equal(originalDigest, SHA256.HashData(await File.ReadAllBytesAsync(backup.File)));
            Assert.Equal(temporaryBefore, await TemporaryCountAsync(connection));
            await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => service.RehearseRecoveryAsync(backup.File, "clave ficticia incorrecta para ensayo", default));
            var corrupted = Path.Combine(directory, "altered.scaibackup"); encrypted[^1] ^= 1; await File.WriteAllBytesAsync(corrupted, encrypted);
            await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => service.RehearseRecoveryAsync(corrupted, Password, default));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RehearseRecoveryAsync(backup.File, Password, cancelled.Token));
            Assert.Equal(temporaryBefore, await TemporaryCountAsync(connection)); Assert.True(BackupInventoryReader.Same(before, await InventoryAsync(connection)));
        }
        finally { RemoveOwnedFiles(directory); }
    }

    [BackupPostgresFact]
    public async Task PreparationRejectsOldApiAndAgentCredentialsWhileKeepingTheOwnerAndContent()
    {
        await using var target = await OwnedRestore.CreateAsync(); await using var db = Database(target.ConnectionString); await db.Database.MigrateAsync();
        using var factory = new AccountApiFactory(postgresConnection: target.ConnectionString, recoveryFixture: true);
        using var client = factory.CreateApiClient(); var account = AccountCredentials.New();
        var owner = await DeviceApiTestSupport.RegisterVerifiedAsync(factory, client, account);
        var unverifiedAccount = AccountCredentials.New(); await factory.RegisterAsync(client, unverifiedAccount);
        var verificationToken = factory.Email.TokenFor(unverifiedAccount.Email, AccountEmailPurpose.EmailVerification);
        var device = await DeviceApiTestSupport.CreateAsync(client, owner.AccessToken);
        var credential = await DeviceApiTestSupport.PairAsync(client, device.PairingCode);
        using (var heartbeat = DeviceApiTestSupport.Heartbeat(credential.DeviceToken))
        using (var alive = await client.SendAsync(heartbeat)) Assert.Equal(HttpStatusCode.NoContent, alive.StatusCode);
        using (var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { account.Email })) Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        var recoveryToken = factory.Email.TokenFor(account.Email, AccountEmailPurpose.PasswordReset);
        var pairingToken = OpaqueToken.Create(); var changeToken = OpaqueToken.Create(); var now = DateTime.UtcNow;
        db.DevicePairingCodes.Add(new() { DeviceId = device.Device.Id, CodeHash = OpaqueToken.Hash(pairingToken), CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(10) });
        db.EmailChangeRequests.Add(new() { UserId = owner.User.Id, CurrentEmail = account.Email, NewEmail = $"new-{Guid.NewGuid():N}@example.invalid",
            CurrentTokenHash = OpaqueToken.Hash(changeToken), NewTokenHash = Hash(), CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(15) });
        db.AccountEmailOutbox.Add(new() { UserId = owner.User.Id, ProtectedPayload = Private, CreatedAtUtc = now, ExpiresAtUtc = now.AddHours(1), NextAttemptAtUtc = now });
        await db.SaveChangesAsync();
        var oldUserVersion = await db.Users.Where(item => item.Id == owner.User.Id).Select(item => item.ConcurrencyStamp).SingleAsync();
        var oldDeviceVersion = await db.Devices.Where(item => item.Id == device.Device.Id).Select(item => item.ConcurrencyStamp).SingleAsync();
        await using var connection = new NpgsqlConnection(target.ConnectionString); await connection.OpenAsync();
        var inventory = await InventoryAsync(connection);
        var report = await RestoredAccessReset.PrepareAsync(connection, target.Name, inventory, default); Assert.True(report.ContentPreserved);
        db.ChangeTracker.Clear();
        Assert.NotEqual(oldUserVersion, await db.Users.Where(item => item.Id == owner.User.Id).Select(item => item.ConcurrencyStamp).SingleAsync());
        Assert.NotEqual(oldDeviceVersion, await db.Devices.Where(item => item.Id == device.Device.Id).Select(item => item.ConcurrencyStamp).SingleAsync());
        Assert.NotNull((await db.Devices.SingleAsync()).RevokedAtUtc); Assert.Empty(await db.AccountEmailOutbox.ToListAsync());
        using (var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", owner.AccessToken))
        using (var denied = await client.SendAsync(request)) Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using (var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = owner.RefreshToken })) Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        using (var heartbeat = DeviceApiTestSupport.Heartbeat(credential.DeviceToken))
        using (var denied = await client.SendAsync(heartbeat)) Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using (var verification = await client.PostAsJsonAsync("/api/auth/verify-email", new { token = verificationToken })) Assert.Equal(HttpStatusCode.BadRequest, verification.StatusCode);
        using (var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new { token = recoveryToken, password = AccountPasswordChangeTests.NewPassword })) Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        using (var pair = await client.PostAsJsonAsync("/api/agent/pair", new { pairingCode = pairingToken, agentVersion = "test" })) Assert.Equal(HttpStatusCode.BadRequest, pair.StatusCode);
        using (var change = await client.PostAsJsonAsync("/api/account/email/confirm-current", new { token = changeToken, confirmed = true })) Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
        await AccountSessionManagementTests.LoginAsync(client, account);
    }

    [BackupPostgresFact]
    public async Task DeleteFailureRollsBackEarlierAccessRemovals()
    {
        await using var target = await OwnedRestore.CreateAsync(); await using var db = Database(target.ConnectionString); await SeedAsync(db);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION public.scai_fail_recovery() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Fixture recovery failure'; END; $$;
            CREATE TRIGGER scai_fail_recovery BEFORE DELETE ON public.account_action_tokens
            FOR EACH ROW EXECUTE FUNCTION public.scai_fail_recovery();
            """);
        await using var connection = new NpgsqlConnection(target.ConnectionString); await connection.OpenAsync(); var before = await InventoryAsync(connection);
        await Assert.ThrowsAsync<PostgresException>(() => RestoredAccessReset.PrepareAsync(connection, target.Name, before, default));
        Assert.True(BackupInventoryReader.Same(before, await InventoryAsync(connection)));
    }

    [BackupPostgresFact]
    public async Task ChangedBusinessContentAbortsThePreparationAndRollsEverythingBack()
    {
        await using var target = await OwnedRestore.CreateAsync(); await using var db = Database(target.ConnectionString); await SeedAsync(db);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION public.scai_alter_recovery_content() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN UPDATE public.security_analyses SET "Score" = "Score" + 1; RETURN OLD; END; $$;
            CREATE TRIGGER scai_alter_recovery_content AFTER DELETE ON public.user_sessions
            FOR EACH ROW EXECUTE FUNCTION public.scai_alter_recovery_content();
            """);
        await using var connection = new NpgsqlConnection(target.ConnectionString); await connection.OpenAsync(); var before = await InventoryAsync(connection);
        await Assert.ThrowsAsync<InvalidDataException>(() => RestoredAccessReset.PrepareAsync(connection, target.Name, before, default));
        Assert.True(BackupInventoryReader.Same(before, await InventoryAsync(connection)));
    }

    [BackupPostgresFact]
    public async Task UnexpectedSchemaOrDatabaseIdentityIsRejectedBeforeAnyChanges()
    {
        await using var target = await OwnedRestore.CreateAsync(); await using var db = Database(target.ConnectionString); await SeedAsync(db);
        await using var connection = new NpgsqlConnection(target.ConnectionString); await connection.OpenAsync(); var before = await InventoryAsync(connection);
        await Assert.ThrowsAsync<InvalidOperationException>(() => RestoredAccessReset.PrepareAsync(connection, "scai_restore_test_" + Guid.NewGuid().ToString("N"), before, default));
        Assert.True(BackupInventoryReader.Same(before, await InventoryAsync(connection)));
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE public.unknown_auth_material (id uuid PRIMARY KEY)");
        var unexpected = await InventoryAsync(connection);
        await Assert.ThrowsAsync<InvalidDataException>(() => RestoredAccessReset.PrepareAsync(connection, target.Name, unexpected, default));
        Assert.True(BackupInventoryReader.Same(unexpected, await InventoryAsync(connection)));
        await db.Database.ExecuteSqlRawAsync("DROP TABLE public.unknown_auth_material");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE public.devices ADD COLUMN unknown_credential text");
        var alteredColumns = await InventoryAsync(connection);
        await Assert.ThrowsAsync<InvalidDataException>(() => RestoredAccessReset.PrepareAsync(connection, target.Name, alteredColumns, default));
        Assert.True(BackupInventoryReader.Same(alteredColumns, await InventoryAsync(connection)));
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE public.devices DROP COLUMN unknown_credential");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO public.\"__EFMigrationsHistory\" (\"MigrationId\",\"ProductVersion\") VALUES ('20990101000000_UnreviewedSchema','8.0.11')");
        var future = await InventoryAsync(connection);
        await Assert.ThrowsAsync<InvalidDataException>(() => RestoredAccessReset.PrepareAsync(connection, target.Name, future, default));
        Assert.True(BackupInventoryReader.Same(future, await InventoryAsync(connection)));
        await using var source = new NpgsqlConnection(SourceConnection()); await source.OpenAsync();
        await using (var sourceDb = Database(SourceConnection())) await sourceDb.Database.MigrateAsync();
        var original = await InventoryAsync(source);
        await Assert.ThrowsAsync<InvalidOperationException>(() => RestoredAccessReset.PrepareAsync(source, target.Name, original, default));
        Assert.True(BackupInventoryReader.Same(original, await InventoryAsync(source)));
    }

    private static void RemoveOwnedFiles(string directory)
    {
        var root = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecurityCenterAI", "Backups"));
        if (Path.GetDirectoryName(Path.GetFullPath(directory)) != root || !Regex.IsMatch(Path.GetFileName(directory), "^test_[a-f0-9]{32}$")) throw new InvalidOperationException("Unsafe rehearsal fixture cleanup.");
        if (!Directory.Exists(directory)) return;
        foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
        Directory.Delete(directory, recursive: false);
    }
    private sealed class OwnedRestore(string name, string admin) : IAsyncDisposable
    {
        public string Name => name;
        public string ConnectionString => new NpgsqlConnectionStringBuilder(admin) { Database = name }.ConnectionString;
        public static async Task<OwnedRestore> CreateAsync()
        {
            var admin = new NpgsqlConnectionStringBuilder(SourceConnection()) { Database = "postgres", Pooling = false }.ConnectionString;
            var name = "scai_restore_test_" + Guid.NewGuid().ToString("N");
            await using var connection = new NpgsqlConnection(admin); await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\" TEMPLATE template0", connection); await create.ExecuteNonQueryAsync();
            await using var restrict = new NpgsqlCommand($"REVOKE CONNECT ON DATABASE \"{name}\" FROM PUBLIC", connection); await restrict.ExecuteNonQueryAsync();
            return new(name, admin);
        }
        public async ValueTask DisposeAsync()
        {
            if (!Regex.IsMatch(name, "^scai_restore_test_[a-f0-9]{32}$")) throw new InvalidOperationException("Unsafe rehearsal database cleanup.");
            await using var connection = new NpgsqlConnection(admin); await connection.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", connection); await drop.ExecuteNonQueryAsync();
        }
    }
}
