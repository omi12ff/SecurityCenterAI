using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Tests;

public sealed class AccountEmailQueueTests
{
    private const string Private = "NEVER_LOG_SMTP_RECIPIENT_OR_TOKEN";
    private static AccountEmail Notice => new(Private + "@example.invalid", "", AccountEmailPurpose.PasswordChanged);

    [Fact]
    public void PayloadIsAuthenticatedAndBoundToItsOwnerAndRow()
    {
        var protector = new AccountEmailProtection(new EphemeralDataProtectionProvider());
        var owner = Guid.NewGuid(); var id = Guid.NewGuid();
        var message = new AccountEmail(Notice.Recipient, OpaqueToken.Create(), AccountEmailPurpose.PasswordReset);
        var payload = protector.Protect(owner, id, message);
        Assert.DoesNotContain(message.Token, payload); Assert.DoesNotContain(Private, payload);
        Assert.Equal(message, protector.Unprotect(owner, id, payload));
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(Guid.NewGuid(), id, payload));
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(owner, Guid.NewGuid(), payload));
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(owner, id, payload[..^8] + "abcdefgh"));
        Assert.Throws<InvalidOperationException>(() => protector.Protect(owner, id, message with { Token = "invalid" }));
        Assert.Throws<InvalidOperationException>(() => protector.Protect(owner, id, Notice with { Token = "must-be-empty" }));
        Assert.Throws<InvalidOperationException>(() => protector.Protect(owner, id, Notice with { Purpose = (AccountEmailPurpose)900 }));
        Assert.Throws<InvalidOperationException>(() => protector.Protect(owner, id, Notice with { Recipient = "invalid\r\naddress" }));
    }

    [Fact]
    public async Task EnqueueEnlistsWithTheCallerAndAFreshContextCanDeliverOnlyAfterCommit()
    {
        using var harness = new Harness(); await harness.SeedAsync();
        await using (var db = harness.Open())
        {
            await harness.Sender(db).QueueAsync(harness.UserId, Notice, default);
            await using (var other = harness.Open()) Assert.Empty(await other.AccountEmailOutbox.ToListAsync());
            Assert.Single(db.ChangeTracker.Entries<AccountEmailOutbox>()); await db.SaveChangesAsync();
        }
        Assert.True(await harness.DispatchAsync(new Transport())); Assert.Empty(await harness.RowsAsync());
    }

    [Fact]
    public async Task CancellationAndDiscardedContextCannotCreateAPendingDelivery()
    {
        using var harness = new Harness(); await harness.SeedAsync();
        await using (var db = harness.Open())
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => harness.Sender(db).QueueAsync(harness.UserId, Notice, new(true)));
            Assert.Empty(db.ChangeTracker.Entries<AccountEmailOutbox>());
            await harness.Sender(db).QueueAsync(harness.UserId, Notice, default);
        }
        Assert.Empty(await harness.RowsAsync());
    }

    [Fact]
    public async Task TemporaryFailurePersistsAttemptAndBackoffAcrossDispatcherInstances()
    {
        using var harness = new Harness(); await harness.SeedAsync(); await harness.QueueAsync(Notice);
        var failed = new Transport((_, _) => Task.FromException(new SmtpException(SmtpStatusCode.ServiceNotAvailable, Private)));
        Assert.True(await harness.DispatchAsync(failed));
        var pending = Assert.Single(await harness.RowsAsync()); Assert.Equal(1, pending.Attempts); Assert.Null(pending.LeaseId);
        Assert.DoesNotContain(Private, pending.ProtectedPayload); Assert.Contains(1102, harness.Logs.Events);
        var recovered = new Transport(); Assert.False(await harness.DispatchAsync(recovered)); Assert.Equal(0, recovered.Calls);
        harness.Clock.Advance(TimeSpan.FromSeconds(1)); Assert.True(await harness.DispatchAsync(recovered));
        Assert.Equal(1, recovered.Calls); Assert.Empty(await harness.RowsAsync()); harness.Logs.AssertSafe();
    }

    [Theory]
    [InlineData(SmtpStatusCode.MailboxUnavailable)]
    [InlineData(SmtpStatusCode.SyntaxError)]
    public async Task PermanentFailuresAreRemovedAndDoNotBlockTheNextMessage(SmtpStatusCode code)
    {
        using var harness = new Harness(); await harness.SeedAsync(); await harness.QueueAsync(Notice); await harness.QueueAsync(Notice);
        var transport = new Transport((attempt, _) => attempt == 1 ? Task.FromException(new SmtpException(code, Private)) : Task.CompletedTask);
        Assert.True(await harness.DispatchAsync(transport)); Assert.True(await harness.DispatchAsync(transport));
        Assert.Equal(2, transport.Calls); Assert.Empty(await harness.RowsAsync()); Assert.Contains(1101, harness.Logs.Events); harness.Logs.AssertSafe();
    }

    [Fact]
    public async Task TotalAttemptsAreBoundedEvenWhenTheDispatcherRestarts()
    {
        using var harness = new Harness(new() { DeliveryAttempts = 2 }); await harness.SeedAsync(); await harness.QueueAsync(Notice);
        var transport = new Transport((_, _) => Task.FromException(new SmtpException(SmtpStatusCode.GeneralFailure, Private)));
        await harness.DispatchAsync(transport); harness.Clock.Advance(TimeSpan.FromSeconds(1)); await harness.DispatchAsync(transport);
        Assert.Equal(2, transport.Calls); Assert.Empty(await harness.RowsAsync()); harness.Logs.AssertSafe();
    }

    [Fact]
    public async Task ShutdownKeepsEncryptedWorkAndAWorkerCanReclaimItAfterTheLease()
    {
        using var harness = new Harness(); await harness.SeedAsync(); await harness.QueueAsync(Notice);
        using var cancellation = new CancellationTokenSource();
        var interrupted = new Transport((_, token) => { cancellation.Cancel(); return Task.FromCanceled(token); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.DispatchAsync(interrupted, cancellation.Token));
        var pending = Assert.Single(await harness.RowsAsync()); Assert.NotNull(pending.LeaseId); Assert.Equal(1, pending.Attempts);
        var restarted = new Transport(); Assert.False(await harness.DispatchAsync(restarted));
        harness.Clock.Advance(AccountEmailDispatcher.LeaseDuration.Add(TimeSpan.FromSeconds(1)));
        Assert.True(await harness.DispatchAsync(restarted)); Assert.Equal(1, restarted.Calls); Assert.Empty(await harness.RowsAsync());
    }

    [Fact]
    public async Task ExpiredAndExhaustedRowsAreRemovedWithoutSmtp()
    {
        using var harness = new Harness(new() { QueueLifetimeMinutes = 1 }); await harness.SeedAsync(); await harness.QueueAsync(Notice);
        harness.Clock.Advance(TimeSpan.FromMinutes(1)); var transport = new Transport();
        Assert.True(await harness.DispatchAsync(transport)); Assert.Equal(0, transport.Calls);
        await harness.QueueAsync(Notice);
        await using (var db = harness.Open()) { var row = await db.AccountEmailOutbox.SingleAsync(); row.Attempts = 3; await db.SaveChangesAsync(); }
        Assert.True(await harness.DispatchAsync(transport)); Assert.Equal(0, transport.Calls); Assert.Empty(await harness.RowsAsync()); harness.Logs.AssertSafe();
    }

    [Theory]
    [InlineData("consumed")]
    [InlineData("expired")]
    [InlineData("replaced")]
    [InlineData("different-recipient")]
    public async Task InvalidRecoveryMaterialIsNotSent(string reason)
    {
        using var harness = new Harness(); await harness.SeedAsync(); var token = OpaqueToken.Create();
        await using (var db = harness.Open())
        {
            db.AccountActionTokens.Add(new() { UserId = harness.UserId, TokenHash = OpaqueToken.Hash(reason == "replaced" ? OpaqueToken.Create() : token),
                Purpose = AccountService.PasswordResetPurpose, CreatedAtUtc = harness.Now, ExpiresAtUtc = reason == "expired" ? harness.Now : harness.Now.AddMinutes(15),
                ConsumedAtUtc = reason == "consumed" ? harness.Now : null }); await db.SaveChangesAsync();
        }
        await harness.QueueAsync(new(reason == "different-recipient" ? "other@example.invalid" : Notice.Recipient, token, AccountEmailPurpose.PasswordReset));
        var transport = new Transport(); Assert.True(await harness.DispatchAsync(transport)); Assert.Equal(0, transport.Calls); Assert.Empty(await harness.RowsAsync());
    }

    [Fact]
    public async Task LiveVerificationIsSentButAlreadyVerifiedAddressesAreSkipped()
    {
        using var harness = new Harness(); await harness.SeedAsync(); var token = OpaqueToken.Create();
        await using (var db = harness.Open()) { db.AccountActionTokens.Add(new() { UserId = harness.UserId, TokenHash = OpaqueToken.Hash(token),
            Purpose = AccountService.VerificationPurpose, CreatedAtUtc = harness.Now, ExpiresAtUtc = harness.Now.AddHours(1) }); await db.SaveChangesAsync(); }
        var message = new AccountEmail(Notice.Recipient, token, AccountEmailPurpose.EmailVerification);
        await harness.QueueAsync(message); var transport = new Transport(); await harness.DispatchAsync(transport); Assert.Equal(1, transport.Calls);
        await harness.QueueAsync(message);
        await using (var db = harness.Open()) { (await db.Users.SingleAsync()).EmailVerified = true; await db.SaveChangesAsync(); }
        await harness.DispatchAsync(transport); Assert.Equal(1, transport.Calls); Assert.Empty(await harness.RowsAsync());
    }

    [Fact]
    public async Task EmailChangesRequireTheCurrentRequestAndAnUnconfirmedAddress()
    {
        using var harness = new Harness(); await harness.SeedAsync(); var current = OpaqueToken.Create(); var next = OpaqueToken.Create();
        await using (var db = harness.Open()) { db.EmailChangeRequests.Add(new() { UserId = harness.UserId, CurrentEmail = Notice.Recipient,
            NewEmail = "next@example.invalid", CurrentTokenHash = OpaqueToken.Hash(current), NewTokenHash = OpaqueToken.Hash(next),
            CreatedAtUtc = harness.Now, ExpiresAtUtc = harness.Now.AddMinutes(15) }); await db.SaveChangesAsync(); }
        await harness.QueueAsync(new(Notice.Recipient, current, AccountEmailPurpose.EmailChangeCurrent));
        var transport = new Transport(); await harness.DispatchAsync(transport); Assert.Equal(1, transport.Calls);
        await using (var db = harness.Open()) { (await db.EmailChangeRequests.SingleAsync()).NewConfirmedAtUtc = harness.Now; await db.SaveChangesAsync(); }
        await harness.QueueAsync(new("next@example.invalid", next, AccountEmailPurpose.EmailChangeNew));
        await harness.DispatchAsync(transport); Assert.Equal(1, transport.Calls);
        await using (var db = harness.Open()) { db.EmailChangeRequests.Remove(await db.EmailChangeRequests.SingleAsync()); await db.SaveChangesAsync(); }
        await harness.QueueAsync(new(Notice.Recipient, current, AccountEmailPurpose.EmailChangeCurrent));
        await harness.DispatchAsync(transport); Assert.Equal(1, transport.Calls); Assert.Empty(await harness.RowsAsync());
    }

    [Fact]
    public async Task CorruptedAndReboundPayloadsAreRemovedWithoutLeakingTheirMaterial()
    {
        using var harness = new Harness(); await harness.SeedAsync(); await harness.QueueAsync(Notice);
        await using (var db = harness.Open()) { var row = await db.AccountEmailOutbox.SingleAsync(); row.ProtectedPayload += "tampered"; await db.SaveChangesAsync(); }
        var transport = new Transport(); await harness.DispatchAsync(transport); Assert.Equal(0, transport.Calls); harness.Logs.AssertSafe();
        await harness.QueueAsync(Notice);
        await using (var db = harness.Open()) { var row = await db.AccountEmailOutbox.SingleAsync(); row.UserId = Guid.NewGuid(); await db.SaveChangesAsync(); }
        await harness.DispatchAsync(transport); Assert.Equal(0, transport.Calls); Assert.Empty(await harness.RowsAsync()); harness.Logs.AssertSafe();
    }

    private sealed class Harness(EmailOptions? settings = null) : IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        public Guid UserId { get; } = Guid.NewGuid();
        public TestClock Clock { get; } = new();
        public DateTime Now => Clock.GetUtcNow().UtcDateTime;
        public AccountEmailProtection Protection { get; } = new(new EphemeralDataProtectionProvider());
        public Logs Logs { get; } = new();
        public AppDbContext Open() => new(_options);
        public QueuedAccountEmailSender Sender(AppDbContext db) => new(db, Protection, Clock, Options.Create(settings ?? new()));
        public async Task SeedAsync() { await using var db = Open(); db.Users.Add(new() { Id = UserId, Name = "Ficticio", Email = Notice.Recipient, PasswordHash = "synthetic" }); await db.SaveChangesAsync(); }
        public async Task QueueAsync(AccountEmail message) { await using var db = Open(); await Sender(db).QueueAsync(UserId, message, default); await db.SaveChangesAsync(); }
        public async Task<List<AccountEmailOutbox>> RowsAsync() { await using var db = Open(); return await db.AccountEmailOutbox.AsNoTracking().ToListAsync(); }
        public async Task<bool> DispatchAsync(Transport transport, CancellationToken token = default) { await using var db = Open(); return await new AccountEmailDispatcher(db, Protection, transport, Options.Create(settings ?? new()), Clock, Logs).ProcessOneAsync(token); }
        public void Dispose() { using var db = Open(); db.Database.EnsureDeleted(); }
    }

    private sealed class Transport(Func<int, CancellationToken, Task>? deliver = null) : IAccountEmailTransport
    {
        public int Calls { get; private set; }
        public Task SendAsync(AccountEmail message, CancellationToken cancellationToken)
        { Calls++; return deliver?.Invoke(Calls, cancellationToken) ?? Task.CompletedTask; }
    }

    private sealed class Logs : ILogger<AccountEmailWorker>
    {
        public List<int> Events { get; } = [];
        private readonly List<string> _messages = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { Assert.Null(exception); Events.Add(id.Id); _messages.Add(formatter(state, exception)); }
        public void AssertSafe() { Assert.NotEmpty(_messages); Assert.All(_messages, message => Assert.DoesNotContain(Private, message)); }
    }
}
