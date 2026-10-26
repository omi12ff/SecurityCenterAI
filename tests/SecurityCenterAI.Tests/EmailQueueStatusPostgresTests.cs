using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SecurityCenterAI.Api.Services.Operations;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

[CollectionDefinition("EmailQueuePostgres", DisableParallelization = true)]
public sealed class EmailQueuePostgresCollection;

[Collection("EmailQueuePostgres")]
public sealed class EmailQueueStatusPostgresTests
{
    private const string Private = "NEVER_DIAGNOSTICS_MATERIAL";
    private static readonly DateTime Now = new(2026, 10, 25, 15, 0, 0, DateTimeKind.Utc);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
    private static string Connection()
    {
        var value = Environment.GetEnvironmentVariable("SECURITY_CENTER_AUDIT_TEST_CONNECTION") ?? throw new InvalidOperationException("Missing isolated connection.");
        var parsed = new NpgsqlConnectionStringBuilder(value);
        if (parsed.Host != "127.0.0.1" || parsed.Port != 5432 || !System.Text.RegularExpressions.Regex.IsMatch(parsed.Database ?? "", "^scai_audit_test_[a-f0-9]{32}$"))
            throw new InvalidOperationException("Unsafe diagnostic test database.");
        return value;
    }
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Connection()).Options);
    private static EmailQueueStatusReader Reader(string? connection = null) => new(new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["ConnectionStrings:PostgreSQL"] = connection ?? Connection(), ["Email:DeliveryAttempts"] = "3" }).Build(), new Clock());
    private static async Task<User> PrepareAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync(); await db.AccountEmailOutbox.ExecuteDeleteAsync();
        var owner = new User { Name = "Queue diagnostic fixture", Email = $"queue-status-{Guid.NewGuid():N}@example.invalid", PasswordHash = Private };
        db.Users.Add(owner); await db.SaveChangesAsync(); return owner;
    }
    private static AccountEmailOutbox Row(Guid ownerId, DateTime? now = null)
    {
        var time = now ?? Now;
        return new() { UserId = ownerId, CreatedAtUtc = time.AddMinutes(-1), ExpiresAtUtc = time.AddHours(1),
            NextAttemptAtUtc = time, ProtectedPayload = Private };
    }
    private static async Task<string> RowsAsync(AppDbContext db) => JsonSerializer.Serialize(
        await db.AccountEmailOutbox.AsNoTracking().OrderBy(item => item.Id).ToListAsync());

    [EmailOutboxPostgresFact]
    public async Task CountersPartitionTheQueueWithoutReadingOrMutatingProtectedMaterial()
    {
        await using var db = Database(); var owner = await PrepareAsync(db);
        var ready = Row(owner.Id); var retry = Row(owner.Id); retry.Attempts = 1; retry.NextAttemptAtUtc = Now.AddSeconds(-5);
        var waiting = Row(owner.Id); waiting.Attempts = 1; waiting.NextAttemptAtUtc = Now.AddSeconds(30);
        var reserved = Row(owner.Id); reserved.Attempts = 3; reserved.LeaseId = Guid.NewGuid(); reserved.LeaseExpiresAtUtc = Now.AddMinutes(2); reserved.ExpiresAtUtc = Now.AddSeconds(-1);
        var expired = Row(owner.Id); expired.CreatedAtUtc = Now.AddMinutes(-12); expired.ExpiresAtUtc = Now;
        var exhausted = Row(owner.Id); exhausted.Attempts = 3;
        var reclaim = Row(owner.Id); reclaim.Attempts = 1; reclaim.LeaseId = Guid.NewGuid(); reclaim.LeaseExpiresAtUtc = Now;
        db.AccountEmailOutbox.AddRange(ready, retry, waiting, reserved, expired, exhausted, reclaim); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var before = await RowsAsync(db); var report = await Reader().ReadAsync(new(), default);
        Assert.Equal(new EmailQueueCounts(7, 3, 1, 1, 1, 1, 3), report.Counts);
        Assert.Equal(720, report.OldestAgeSeconds); Assert.Equal(Now.AddMinutes(-12), report.OldestCreatedAtUtc);
        Assert.Equal(Now.AddSeconds(-5), report.NextAttemptAtUtc); Assert.Equal(Now.AddMinutes(2), report.NextReservationExpiryAtUtc);
        Assert.Equal(new[] { "oldest_age_threshold", "expired_pending", "attempts_exhausted" }, report.Warnings);
        Assert.Equal(before, await RowsAsync(db));
        var json = JsonSerializer.Serialize(report); Assert.DoesNotContain(Private, json);
        Assert.DoesNotContain(owner.Id.ToString(), json); Assert.DoesNotContain(ready.Id.ToString(), json);
    }

    [EmailOutboxPostgresFact]
    public async Task EmptyQueueReturnsZerosAndNoScheduledOrHistoricalDates()
    {
        await using var db = Database(); await PrepareAsync(db);
        var report = await Reader().ReadAsync(new(), default);
        Assert.Equal("ok", report.Status); Assert.Equal(new EmailQueueCounts(0, 0, 0, 0, 0, 0, 0), report.Counts);
        Assert.Null(report.OldestAgeSeconds); Assert.Null(report.NextAttemptAtUtc); Assert.Null(report.NextReservationExpiryAtUtc);
    }

    [EmailOutboxPostgresFact]
    public async Task CliWorksWithoutJwtSmtpOrKeysAndReturnsAttentionWithoutChangingDelivery()
    {
        await using var db = Database(); var owner = await PrepareAsync(db);
        var empty = await RunCliAsync(Connection(), "--email-queue-status"); Assert.Equal(0, empty.ExitCode);
        using (var json = JsonDocument.Parse(empty.Output)) Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
        db.AccountEmailOutbox.Add(Row(owner.Id, DateTime.UtcNow)); await db.SaveChangesAsync(); var before = await RowsAsync(db);
        var warning = await RunCliAsync(Connection(), "--email-queue-status", "--warning-count", "1"); Assert.Equal(3, warning.ExitCode);
        using var report = JsonDocument.Parse(warning.Output); Assert.Equal("attention", report.RootElement.GetProperty("status").GetString());
        Assert.DoesNotContain(Private, warning.Output + warning.Error); Assert.Equal(before, await RowsAsync(db));
        var bad = await RunCliAsync(Connection(), "--email-queue-status", "--warning-count", Private);
        Assert.Equal(2, bad.ExitCode); Assert.DoesNotContain(Private, bad.Output + bad.Error);
    }

    [EmailOutboxPostgresFact]
    public async Task MissingSchemaRefusedConnectionAndTableLocksProduceOnlyFixedErrors()
    {
        await using var db = Database(); await PrepareAsync(db);
        var badConnection = new NpgsqlConnectionStringBuilder(Connection()) { Port = UnusedPort(), Password = Private };
        var refused = await RunCliAsync(badConnection.ConnectionString, "--email-queue-status");
        Assert.Equal(1, refused.ExitCode); Assert.DoesNotContain(Private, refused.Output + refused.Error);
        await using var empty = await EmptyDatabase.CreateAsync();
        var missing = await RunCliAsync(empty.ConnectionString, "--email-queue-status"); Assert.Equal(1, missing.ExitCode);
        Assert.DoesNotContain("account_email_outbox", missing.Error); Assert.DoesNotContain(empty.Name, missing.Error);
        await using var connection = new NpgsqlConnection(Connection()); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("LOCK TABLE account_email_outbox IN ACCESS EXCLUSIVE MODE", connection, transaction)) await command.ExecuteNonQueryAsync();
        var blocked = await RunCliAsync(Connection(), "--email-queue-status"); Assert.Equal(1, blocked.ExitCode);
        Assert.DoesNotContain("55P03", blocked.Error); Assert.DoesNotContain("Npgsql", blocked.Error);
        await transaction.RollbackAsync();
    }

    [EmailOutboxPostgresFact]
    public async Task StatusScriptCombinesLocalProbesAndQueueExitCodesWithoutSendingMail()
    {
        await using var db = Database(); var owner = await PrepareAsync(db);
        await using var probes = new LocalProbes();
        var healthy = await RunStatusAsync(probes.HttpPort, probes.SmtpPort); Assert.Equal(0, healthy.ExitCode);
        Assert.Contains("Total: 0", healthy.Output); Assert.Contains("sin pendientes", healthy.Output);
        db.AccountEmailOutbox.Add(Row(owner.Id, DateTime.UtcNow)); await db.SaveChangesAsync(); var before = await RowsAsync(db);
        var attention = await RunStatusAsync(probes.HttpPort, probes.SmtpPort); Assert.Equal(3, attention.ExitCode);
        Assert.Contains("Total: 1", attention.Output); Assert.Contains("habilitado ahora", attention.Output);
        Assert.Contains("cantidad de pendientes en el umbral", attention.Output);
        var smtpDown = await RunStatusAsync(probes.HttpPort, UnusedPort()); Assert.Equal(1, smtpDown.ExitCode);
        Assert.Contains("Cola de correo persistente: consulta disponible", smtpDown.Output);
        var apiDown = await RunStatusAsync(UnusedPort(), probes.SmtpPort); Assert.Equal(1, apiDown.ExitCode);
        Assert.Contains("API HTTP: no disponible", apiDown.Output); Assert.Contains("Total: 1", apiDown.Output);
        Assert.Equal(before, await RowsAsync(db));
        Assert.NotEmpty(probes.SmtpCommands); Assert.All(probes.SmtpCommands, command => Assert.Contains(command, new[] { "NOOP", "QUIT" }));
        Assert.DoesNotContain(Private, healthy.Output + attention.Output + smtpDown.Output + apiDown.Output);
    }

    [EmailOutboxPostgresFact]
    public async Task StandaloneHelperReportsMissingSchemaWithoutLeakingItsConnection()
    {
        await using var empty = await EmptyDatabase.CreateAsync();
        var result = await RunPowerShellAsync("email-queue-status.ps1", "-Configuration", BuildConfiguration, "-NoBuild", "-DatabaseName", empty.Name);
        Assert.Equal(1, result.ExitCode); Assert.Contains("No se pudo consultar la cola", result.Output);
        Assert.DoesNotContain(empty.Name, result.Output + result.Error); Assert.DoesNotContain("Password", result.Output + result.Error);
    }

    private static string Repository
    {
        get { for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
                if (File.Exists(Path.Combine(folder.FullName, "SecurityCenterAI.sln"))) return folder.FullName;
            throw new InvalidOperationException("Repository not found."); }
    }
    private static string BuildConfiguration => typeof(Program).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
    private static Task<ProcessResult> RunCliAsync(string connection, params string[] args)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(Repository, "src", "SecurityCenterAI.Api") };
        start.ArgumentList.Add(Path.Combine(start.WorkingDirectory, "bin", BuildConfiguration, "net8.0", "SecurityCenterAI.Api.dll"));
        foreach (var arg in args) start.ArgumentList.Add(arg);
        start.Environment["ConnectionStrings__PostgreSQL"] = connection;
        start.Environment["Jwt__Key"] = "invalid"; start.Environment["Email__Host"] = "not-a-local-smtp.example.invalid";
        start.Environment["FrontendUrl"] = "not-a-url";
        return RunAsync(start);
    }
    private static Task<ProcessResult> RunStatusAsync(int httpPort, int smtpPort) => RunPowerShellAsync("status.ps1",
        "-Configuration", BuildConfiguration, "-NoBuild", "-DatabaseName", new NpgsqlConnectionStringBuilder(Connection()).Database!,
        "-Port", httpPort.ToString(), "-SmtpPort", smtpPort.ToString(), "-QueueWarningCount", "1");
    private static Task<ProcessResult> RunPowerShellAsync(string script, params string[] args)
    {
        var start = new ProcessStartInfo("powershell") { WorkingDirectory = Repository };
        foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(Repository, "scripts", script) }.Concat(args)) start.ArgumentList.Add(arg);
        return RunAsync(start);
    }
    private sealed record ProcessResult(int ExitCode, string Output, string Error);
    private static async Task<ProcessResult> RunAsync(ProcessStartInfo start)
    {
        start.UseShellExecute = false; start.CreateNoWindow = true; start.RedirectStandardOutput = true; start.RedirectStandardError = true;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }
        return new(process.ExitCode, await output, await error);
    }
    private static int UnusedPort() { var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port; }

    private sealed class EmptyDatabase(string name, string admin) : IAsyncDisposable
    {
        public string Name => name;
        public string ConnectionString => new NpgsqlConnectionStringBuilder(admin) { Database = name }.ConnectionString;
        public static async Task<EmptyDatabase> CreateAsync()
        {
            var admin = new NpgsqlConnectionStringBuilder(Connection()) { Database = "postgres", Pooling = false }.ConnectionString;
            var name = "scai_audit_test_" + Guid.NewGuid().ToString("N");
            await using var connection = new NpgsqlConnection(admin); await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\" TEMPLATE template0", connection); await create.ExecuteNonQueryAsync();
            return new(name, admin);
        }
        public async ValueTask DisposeAsync()
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^scai_audit_test_[a-f0-9]{32}$")) throw new InvalidOperationException("Unsafe diagnostic database cleanup.");
            await using var connection = new NpgsqlConnection(admin); await connection.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", connection); await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class LocalProbes : IAsyncDisposable
    {
        private readonly TcpListener _http = new(IPAddress.Loopback, 0);
        private readonly TcpListener _smtp = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _httpTask; private readonly Task _smtpTask;
        public ConcurrentQueue<string> SmtpCommands { get; } = new();
        public int HttpPort => ((IPEndPoint)_http.LocalEndpoint).Port;
        public int SmtpPort => ((IPEndPoint)_smtp.LocalEndpoint).Port;
        public LocalProbes() { _http.Start(); _smtp.Start(); _httpTask = HttpAsync(); _smtpTask = SmtpAsync(); }
        private async Task HttpAsync()
        {
            try { while (!_stop.IsCancellationRequested) {
                using var client = await _http.AcceptTcpClientAsync(_stop.Token); await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                var first = await reader.ReadLineAsync(_stop.Token) ?? "";
                while (await reader.ReadLineAsync(_stop.Token) is { Length: > 0 }) { }
                var body = first.Contains("/health/ready") ? "{\"status\":\"ready\"}" : "{\"status\":\"healthy\"}";
                var response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Encoding.ASCII.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}");
                await stream.WriteAsync(response, _stop.Token);
            } } catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        }
        private async Task SmtpAsync()
        {
            try { while (!_stop.IsCancellationRequested) {
                using var client = await _smtp.AcceptTcpClientAsync(_stop.Token); await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                await using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, true) { AutoFlush = true, NewLine = "\r\n" };
                await writer.WriteLineAsync("220 isolated probe");
                while (await reader.ReadLineAsync(_stop.Token) is { } command) {
                    SmtpCommands.Enqueue(command);
                    if (command == "QUIT") break;
                    await writer.WriteLineAsync(command == "NOOP" ? "250 OK" : "500 refused");
                }
            } } catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        }
        public async ValueTask DisposeAsync() { _stop.Cancel(); await Task.WhenAll(_httpTask, _smtpTask); _http.Stop(); _smtp.Stop(); _stop.Dispose(); }
    }
}
