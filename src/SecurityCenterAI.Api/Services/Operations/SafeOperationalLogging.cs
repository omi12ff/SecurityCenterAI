namespace SecurityCenterAI.Api.Services.Operations;

public static class SafeOperationalLogging
{
    // A closed list protects against framework/provider exception and SQL logs,
    // even when configuration enables verbose logging. Review every new category.
    public static void Configure(ILoggingBuilder logging, IServiceCollection services)
    {
        logging.ClearProviders().AddJsonConsole(options =>
        {
            options.IncludeScopes = false;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
        });
        services.PostConfigure<LoggerFilterOptions>(options =>
        {
            options.Rules.Clear();
            options.Rules.Add(new LoggerFilterRule(null, null, LogLevel.Information,
                (_, category, _) => category is
                    "SecurityCenterAI.Api.Services.Operations.OperationalRequestMiddleware" or
                    "SecurityCenterAI.Api.Services.AccountService" or
                    "SecurityCenterAI.Api.Services.AccountPasswordService" or
                    "SecurityCenterAI.Api.Services.AccountEmailChangeService" or
                    "SecurityCenterAI.Api.Services.AccountEmailWorker" or
                    "SecurityCenterAI.Api.Services.RetentionWorker"));
        });
    }

    public static readonly EventId RequestCompleted = new(1000, "request_completed");
    public static readonly EventId RequestFailed = new(1001, "request_failed");
    public static readonly EventId EmailQueueRejected = new(1100, "email_queue_rejected");
    public static readonly EventId EmailDeliveryFailed = new(1101, "email_delivery_failed");
    public static readonly EventId EmailDeliveryRetry = new(1102, "email_delivery_retry");
}
