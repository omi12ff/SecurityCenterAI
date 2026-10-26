using System.Text.Json;
using SecurityCenterAI.Api.Services.Operations;

namespace SecurityCenterAI.Tests;

public sealed class EmailQueueStatusTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 15, 0, 0, DateTimeKind.Utc);
    private static readonly EmailQueueCounts Empty = new(0, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void EmptyQueueHasNoInventedDatesOrWarningsAndAClosedOutputShape()
    {
        var report = EmailQueueStatusReport.Create(Now, Empty, null, null, null, 3, new());
        Assert.Equal("ok", report.Status); Assert.Empty(report.Warnings);
        Assert.Null(report.OldestCreatedAtUtc); Assert.Null(report.OldestAgeSeconds);
        Assert.Null(report.NextAttemptAtUtc); Assert.Null(report.NextReservationExpiryAtUtc);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(new[] { "checkedAtUtc", "counts", "deliveryAttemptLimit", "nextAttemptAtUtc", "nextReservationExpiryAtUtc",
            "oldestAgeSeconds", "oldestCreatedAtUtc", "reportVersion", "status", "thresholds", "warnings" },
            json.RootElement.EnumerateObject().Select(item => item.Name).OrderBy(name => name).ToArray());
        Assert.Equal(new[] { "exhausted", "expired", "ready", "reserved", "retryPending", "total", "waiting" },
            json.RootElement.GetProperty("counts").EnumerateObject().Select(item => item.Name).OrderBy(name => name).ToArray());
    }

    [Fact]
    public void ThresholdBoundariesAreInclusiveAndDoNotTreatNormalRetriesAsFailures()
    {
        var before = EmailQueueStatusReport.Create(Now, new(99, 98, 1, 0, 0, 0, 5), Now.AddSeconds(-599), Now.AddSeconds(-2), null, 3, new());
        Assert.Equal("ok", before.Status); Assert.Equal(599, before.OldestAgeSeconds);
        var boundary = EmailQueueStatusReport.Create(Now, new(100, 99, 1, 0, 0, 0, 5), Now.AddMinutes(-10), Now, null, 3, new());
        Assert.Equal("attention", boundary.Status);
        Assert.Equal(new[] { "queue_count_threshold", "oldest_age_threshold" }, boundary.Warnings);
    }

    [Fact]
    public void ExpiredAndExhaustedMessagesRequestAttentionBelowVolumeAndAgeThresholds()
    {
        var report = EmailQueueStatusReport.Create(Now, new(2, 0, 0, 0, 1, 1, 0), Now.AddSeconds(-30), null, null, 3, new());
        Assert.Equal("attention", report.Status); Assert.Equal(new[] { "expired_pending", "attempts_exhausted" }, report.Warnings);
    }

    [Fact]
    public void ActiveReservationsAreNotAssumedToBeADeadWorkerOrExhaustedDelivery()
    {
        var expires = Now.AddSeconds(90);
        var report = EmailQueueStatusReport.Create(Now, new(1, 0, 0, 1, 0, 0, 0), Now.AddSeconds(-30), null, expires, 3, new());
        Assert.Equal("ok", report.Status); Assert.Null(report.NextAttemptAtUtc); Assert.Equal(expires, report.NextReservationExpiryAtUtc);
        var skewed = EmailQueueStatusReport.Create(Now, new(1, 1, 0, 0, 0, 0, 0), Now.AddSeconds(3), Now, null, 3, new());
        Assert.Equal(0, skewed.OldestAgeSeconds);
    }

    [Fact]
    public void ThresholdOverridesChangeOnlyDiagnosticWarnings()
    {
        Assert.True(EmailQueueStatusCommand.TryParse(["--email-queue-status", "--warning-count", "2", "--warning-age-minutes", "1"], out var thresholds));
        var report = EmailQueueStatusReport.Create(Now, new(2, 2, 0, 0, 0, 0, 1), Now.AddMinutes(-1), Now, null, 5, thresholds);
        Assert.Equal(2, report.Counts.Total); Assert.Equal(5, report.DeliveryAttemptLimit);
        Assert.Equal(new[] { "queue_count_threshold", "oldest_age_threshold" }, report.Warnings);
        Assert.True(EmailQueueStatusCommand.TryParse(["--email-queue-status"], out var defaults)); Assert.Equal(new EmailQueueThresholds(), defaults);
    }

    [Fact]
    public void CommandRejectsUnknownDuplicateMissingAndOutOfRangeArgumentsWithoutUsingTheirValues()
    {
        string[][] invalid = [[], ["--email-queue-status", "--warning-count"], ["--email-queue-status", "--warning-count", "0"],
            ["--email-queue-status", "--warning-count", "1000001"], ["--email-queue-status", "--warning-age-minutes", "10081"],
            ["--email-queue-status", "--warning-count", "1", "--warning-count", "2"],
            ["--email-queue-status", "--private-recipient", "NEVER_ECHO_INPUT"], ["--email-queue-status", "--warning-count", "+2"],
            ["--email-queue-status", "--warning-age-minutes", "-1"], ["--EMAIL-QUEUE-STATUS"]];
        Assert.All(invalid, args => Assert.False(EmailQueueStatusCommand.TryParse(args, out _)));
    }
}
