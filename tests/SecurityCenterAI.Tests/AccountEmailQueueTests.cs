using System.Net.Mail;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Services;

namespace SecurityCenterAI.Tests;

public sealed class AccountEmailQueueTests
{
    private const string Private = "NEVER_LOG_SMTP_RECIPIENT_OR_TOKEN";
    private static AccountEmail Message => new(Private + "@example.invalid", Private, AccountEmailPurpose.PasswordReset);
    private static Channel<PendingAccountEmail> Queue() => Channel.CreateBounded<PendingAccountEmail>(1);

    [Fact]
    public async Task TemporarySmtpFailureRetriesThenSucceedsWithoutLoggingDeliveryMaterial()
    {
        var queue = Queue(); var clock = new TestClock(); var logs = new Logs();
        var transport = new Transport((attempt, _) => attempt == 1 ? Task.FromException(new SmtpException(SmtpStatusCode.ServiceNotAvailable, Private)) : Task.CompletedTask);
        using var worker = Worker(queue, transport, clock, logs);
        await new QueuedAccountEmailSender(queue, clock).SendAsync(Message, default); queue.Writer.Complete();
        await worker.StartAsync(default); await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.Equal(2, transport.Calls); Assert.Contains(logs.Events, item => item == 1102); Assert.DoesNotContain(1101, logs.Events);
        logs.AssertSafe();
    }

    [Fact]
    public async Task PermanentFailureIsNotRetriedAndTheWorkerContinuesWithTheNextMessage()
    {
        var queue = Channel.CreateBounded<PendingAccountEmail>(2); var clock = new TestClock(); var logs = new Logs();
        var transport = new Transport((attempt, _) => attempt == 1 ? Task.FromException(new SmtpException(SmtpStatusCode.MailboxUnavailable, Private)) : Task.CompletedTask);
        using var worker = Worker(queue, transport, clock, logs);
        var sender = new QueuedAccountEmailSender(queue, clock);
        await sender.SendAsync(Message, default); await sender.SendAsync(Message, default); queue.Writer.Complete();
        await worker.StartAsync(default); await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, transport.Calls); Assert.Single(logs.Events); Assert.Equal(1101, logs.Events[0]); logs.AssertSafe();
    }

    [Fact]
    public async Task RetryCountIsBoundedAndStoppingInterruptsPendingBackoff()
    {
        var queue = Queue(); var clock = new TestClock(); var logs = new Logs();
        var transport = new Transport((_, _) => Task.FromException(new SmtpException(SmtpStatusCode.GeneralFailure, Private)));
        using (var worker = Worker(queue, transport, clock, logs, new() { DeliveryAttempts = 2 }))
        {
            await new QueuedAccountEmailSender(queue, clock).SendAsync(Message, default); queue.Writer.Complete();
            await worker.StartAsync(default); await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(8));
            Assert.Equal(2, transport.Calls); Assert.Contains(1101, logs.Events);
        }
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waitingTransport = new Transport((_, _) => { waiting.TrySetResult(); return Task.FromException(new SmtpException(Private)); });
        var waitingQueue = Queue(); await new QueuedAccountEmailSender(waitingQueue, clock).SendAsync(Message, default);
        using var stopping = Worker(waitingQueue, waitingTransport, clock, logs, new() { RetryDelaySeconds = 30 });
        await stopping.StartAsync(default); await waiting.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await stopping.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, waitingTransport.Calls); logs.AssertSafe();
    }

    [Fact]
    public async Task FullQueueRejectsWithoutWritingAndExpiredMaterialIsDiscardedWithoutSmtp()
    {
        var queue = Queue(); var clock = new TestClock(); var logs = new Logs();
        var sender = new QueuedAccountEmailSender(queue, clock);
        await sender.SendAsync(Message, default);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(Message, default));
        Assert.DoesNotContain(Private, exception.Message);
        clock.Advance(TimeSpan.FromMinutes(5)); queue.Writer.Complete();
        var transport = new Transport((_, _) => Task.CompletedTask);
        using var worker = Worker(queue, transport, clock, logs);
        await worker.StartAsync(default); await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(0, transport.Calls); Assert.Contains(1101, logs.Events); logs.AssertSafe();
    }

    private static AccountEmailWorker Worker(Channel<PendingAccountEmail> queue, Transport transport, TestClock clock, Logs logs, EmailOptions? settings = null) =>
        new(queue, transport, Options.Create(settings ?? new()), clock, logs);

    private sealed class Transport(Func<int, CancellationToken, Task> deliver) : IAccountEmailTransport
    {
        public int Calls { get; private set; }
        public Task SendAsync(AccountEmail message, CancellationToken cancellationToken) => deliver(++Calls, cancellationToken);
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
