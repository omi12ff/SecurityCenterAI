using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class EmailOutboxPostgresFactAttribute : FactAttribute
{
    public EmailOutboxPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SECURITY_CENTER_AUDIT_TEST_CONNECTION")))
            Skip = "Run scripts/test-email-outbox.ps1 with an isolated fictitious PostgreSQL database.";
    }
}

[Collection("EmailQueuePostgres")]
public sealed class AccountEmailOutboxPostgresTests
{
    private static AccountApiFactory Factory(IDataProtectionProvider? protection = null) => new(postgresConnection:
        Environment.GetEnvironmentVariable("SECURITY_CENTER_AUDIT_TEST_CONNECTION") ?? throw new InvalidOperationException("Missing isolated connection."),
        durableEmail: true, emailProtectionProvider: protection);
    private static async Task MigrateAsync(AccountApiFactory factory)
    { using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(); }
    private static AccountEmailDispatcher Dispatcher(AccountApiFactory factory, IServiceScope scope, IAccountEmailTransport transport) => new(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(), factory.Services.GetRequiredService<AccountEmailProtection>(), transport,
        Options.Create(new EmailOptions()), factory.Clock, NullLogger<AccountEmailWorker>.Instance);

    [EmailOutboxPostgresFact]
    public async Task AnOutboxInsertFailureRollsBackRegistrationAndAllItsAccessMaterial()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await RejectInsertsAsync(db);
        var account = AccountCredentials.New();
        try
        {
            using var response = await client.PostAsJsonAsync("/api/auth/register", new { account.Name, account.Email, account.Password });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.False(await db.Users.AnyAsync(item => item.Email == account.Email));
            Assert.DoesNotContain(account.Password, await response.Content.ReadAsStringAsync());
        }
        finally { await AllowInsertsAsync(db); }
        var owner = await factory.RegisterAsync(client, account);
        var row = Assert.Single(await db.AccountEmailOutbox.AsNoTracking().Where(item => item.UserId == owner.User.Id).ToListAsync());
        Assert.DoesNotContain(account.Email, row.ProtectedPayload);
        var message = factory.Services.GetRequiredService<AccountEmailProtection>().Unprotect(owner.User.Id, row.Id, row.ProtectedPayload);
        Assert.DoesNotContain(message.Token, row.ProtectedPayload); Assert.Equal(account.Email, message.Recipient);
        Assert.Single(await db.AccountActionTokens.Where(item => item.UserId == owner.User.Id).ToListAsync());
    }

    [EmailOutboxPostgresFact]
    public async Task NoticeInsertFailureCannotPartiallyChangePasswordOrCreateAnEmailChange()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(item => item.Id == owner.User.Id); user.EmailVerified = true; await db.SaveChangesAsync();
        factory.Clock.Advance(TimeSpan.FromMinutes(2)); await RejectInsertsAsync(db);
        try
        {
            using var changed = await AccountPasswordChangeTests.ChangeAsync(client, owner, account.Password);
            Assert.Equal(HttpStatusCode.InternalServerError, changed.StatusCode);
            using var request = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/auth/me", owner.AccessToken);
            using var active = await client.SendAsync(request); Assert.Equal(HttpStatusCode.OK, active.StatusCode);
            using var email = await AccountEmailChangeTests.PostAsync(client, "request", owner.AccessToken,
                new { newEmail = AccountCredentials.New().Email, currentPassword = account.Password, confirmed = true });
            Assert.Equal(HttpStatusCode.InternalServerError, email.StatusCode);
            Assert.False(await db.EmailChangeRequests.AnyAsync(item => item.UserId == owner.User.Id));
            Assert.Single(await db.AccountEmailOutbox.Where(item => item.UserId == owner.User.Id).ToListAsync());
        }
        finally { await AllowInsertsAsync(db); }
        await AccountSessionManagementTests.LoginAsync(client, account);
    }

    [EmailOutboxPostgresFact]
    public async Task ConcurrentWorkersCannotSendTheSameReservedMessage()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        // Retire only this isolated database's synthetic users from previous cases,
        // so the two workers have one unambiguous candidate.
        await ClearIsolatedOutboxAsync(factory);
        var owner = await factory.RegisterAsync(client, AccountCredentials.New());
        using var firstScope = factory.Services.CreateScope(); using var secondScope = factory.Services.CreateScope();
        var transport = new BlockingTransport();
        var first = Dispatcher(factory, firstScope, transport).ProcessOneAsync(default);
        await transport.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { Assert.False(await Dispatcher(factory, secondScope, transport).ProcessOneAsync(default)); }
        finally { transport.Release.TrySetResult(); }
        Assert.True(await first); Assert.Equal(1, transport.Calls);
        var db = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.AccountEmailOutbox.AnyAsync(item => item.UserId == owner.User.Id));
    }

    [EmailOutboxPostgresFact]
    public async Task AccountExportExcludesCiphertextAndDeletingTheOwnerCascadesOnlyTheirPendingMail()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory);
        var account = AccountCredentials.New(); var owner = await factory.RegisterAsync(client, account);
        var foreign = await factory.RegisterAsync(client, AccountCredentials.New());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.AccountEmailOutbox.AsNoTracking().SingleAsync(item => item.UserId == owner.User.Id);
        using var exportRequest = AccountApiFactory.AuthorizedRequest(HttpMethod.Get, "/api/account/export", owner.AccessToken);
        using var exported = await client.SendAsync(exportRequest); Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        var json = await exported.Content.ReadAsStringAsync(); Assert.DoesNotContain(row.ProtectedPayload, json); Assert.DoesNotContain("protectedPayload", json);
        using var deletion = AccountApiFactory.AuthorizedRequest(HttpMethod.Delete, "/api/account", owner.AccessToken);
        deletion.Content = JsonContent.Create(new { account.Password, confirmation = "ELIMINAR" });
        using var removed = await client.SendAsync(deletion); Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.False(await db.AccountEmailOutbox.AnyAsync(item => item.UserId == owner.User.Id));
        Assert.True(await db.AccountEmailOutbox.AnyAsync(item => item.UserId == foreign.User.Id));
    }

    [EmailOutboxPostgresFact]
    public async Task RestartingTheApiAndReopeningTheDpapiKeyRingPreservesQueuedDelivery()
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("This check requires Windows DPAPI.");
        using var keys = new OwnedEmailKeyRing();
        Guid userId;
        using (var first = Factory(keys.Open()))
        {
            using var client = first.CreateApiClient(); await MigrateAsync(first); await ClearIsolatedOutboxAsync(first);
            userId = (await first.RegisterAsync(client, AccountCredentials.New())).User.Id;
        }
        keys.AssertEncrypted();
        using var restarted = Factory(keys.Open()); using var secondClient = restarted.CreateApiClient();
        using var scope = restarted.Services.CreateScope(); var transport = new RecordingTransport();
        Assert.True(await Dispatcher(restarted, scope, transport).ProcessOneAsync(default)); Assert.Equal(1, transport.Calls);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.AccountEmailOutbox.AnyAsync(item => item.UserId == userId));
    }

    [EmailOutboxPostgresFact]
    public async Task AStoppedWorkerCanResumeAfterItsLeaseAndDowngradeRefusesPendingMail()
    {
        using var factory = Factory(); using var client = factory.CreateApiClient(); await MigrateAsync(factory); await ClearIsolatedOutboxAsync(factory);
        var owner = await factory.RegisterAsync(client, AccountCredentials.New());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.AccountEmailOutbox.SingleAsync(item => item.UserId == owner.User.Id);
        row.LeaseId = Guid.NewGuid(); row.LeaseExpiresAtUtc = factory.Clock.GetUtcNow().UtcDateTime.Add(AccountEmailDispatcher.LeaseDuration);
        row.Attempts = 1; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Assert.ThrowsAnyAsync<Exception>(() => db.GetService<IMigrator>().MigrateAsync("20261024042930_LinkMessageDailyUsage"));
        Assert.True(await db.AccountEmailOutbox.AnyAsync(item => item.UserId == owner.User.Id));
        var transport = new RecordingTransport(); Assert.False(await Dispatcher(factory, scope, transport).ProcessOneAsync(default));
        factory.Clock.Advance(AccountEmailDispatcher.LeaseDuration.Add(TimeSpan.FromSeconds(1)));
        Assert.True(await Dispatcher(factory, scope, transport).ProcessOneAsync(default)); Assert.Equal(1, transport.Calls);
    }

    private static async Task ClearIsolatedOutboxAsync(AccountApiFactory factory)
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var name = db.Database.GetDbConnection().Database;
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^scai_audit_test_[a-f0-9]{32}$")) throw new InvalidOperationException("Unsafe test cleanup target.");
        await db.AccountEmailOutbox.ExecuteDeleteAsync();
    }
    private static Task RejectInsertsAsync(AppDbContext db) => db.Database.ExecuteSqlRawAsync("""
        CREATE FUNCTION public.scai_reject_outbox() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'Isolated outbox insert failure'; END; $$;
        CREATE TRIGGER scai_reject_outbox BEFORE INSERT ON public.account_email_outbox
        FOR EACH ROW EXECUTE FUNCTION public.scai_reject_outbox();
        """);
    private static Task AllowInsertsAsync(AppDbContext db) => db.Database.ExecuteSqlRawAsync(
        "DROP TRIGGER scai_reject_outbox ON public.account_email_outbox; DROP FUNCTION public.scai_reject_outbox();");
    private sealed class RecordingTransport : IAccountEmailTransport
    { public int Calls { get; private set; } public Task SendAsync(AccountEmail message, CancellationToken cancellationToken) { Calls++; return Task.CompletedTask; } }
    private sealed class BlockingTransport : IAccountEmailTransport
    {
        public int Calls { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task SendAsync(AccountEmail message, CancellationToken cancellationToken)
        { Calls++; Started.TrySetResult(); await Release.Task.WaitAsync(cancellationToken); }
    }
}

internal sealed class OwnedEmailKeyRing : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SecurityCenterAI_EmailKeys_" + Guid.NewGuid().ToString("N"));
    public IDataProtectionProvider Open()
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("DPAPI requires Windows.");
        return DataProtectionProvider.Create(new DirectoryInfo(_directory), builder =>
        {
            if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("DPAPI requires Windows.");
            builder.SetApplicationName("SecurityCenterAI.Outbox.Tests").ProtectKeysWithDpapi(protectToLocalMachine: false);
        });
    }
    public void AssertEncrypted()
    {
        var files = Directory.GetFiles(_directory, "key-*.xml"); Assert.NotEmpty(files);
        Assert.All(files, file => { var xml = File.ReadAllText(file); Assert.Contains("encryptedSecret", xml); Assert.Contains("DpapiXmlDecryptor", xml); });
    }
    public void Dispose()
    {
        var full = Path.GetFullPath(_directory); var root = Path.GetFullPath(Path.GetTempPath());
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
            !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(full), "^SecurityCenterAI_EmailKeys_[a-f0-9]{32}$"))
            throw new InvalidOperationException("Unsafe test key directory.");
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
}
