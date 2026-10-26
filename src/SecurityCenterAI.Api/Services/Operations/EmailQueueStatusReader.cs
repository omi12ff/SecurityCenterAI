using System.Data;
using Npgsql;

namespace SecurityCenterAI.Api.Services.Operations;

public sealed record EmailQueueThresholds(int WarningCount = 100, int WarningAgeMinutes = 10)
{
    public void Validate()
    {
        if (WarningCount is < 1 or > 1_000_000 || WarningAgeMinutes is < 1 or > 10_080)
            throw new ArgumentOutOfRangeException(nameof(EmailQueueThresholds));
    }
}

// Ready, Waiting, Reserved, Expired and Exhausted partition Total. RetryPending is a subset.
public sealed record EmailQueueCounts(long Total, long Ready, long Waiting, long Reserved,
    long Expired, long Exhausted, long RetryPending);

public sealed record EmailQueueStatusReport(int ReportVersion, string Status, DateTime CheckedAtUtc,
    EmailQueueCounts Counts, DateTime? OldestCreatedAtUtc, long? OldestAgeSeconds,
    DateTime? NextAttemptAtUtc, DateTime? NextReservationExpiryAtUtc,
    int DeliveryAttemptLimit, EmailQueueThresholds Thresholds, IReadOnlyList<string> Warnings)
{
    public static EmailQueueStatusReport Create(DateTime now, EmailQueueCounts counts, DateTime? oldest,
        DateTime? nextAttempt, DateTime? nextReservationExpiry, int attemptLimit, EmailQueueThresholds thresholds)
    {
        thresholds.Validate();
        var age = oldest.HasValue ? (long?)Math.Max(0, Math.Floor((now - oldest.Value).TotalSeconds)) : null;
        var warnings = new List<string>();
        if (counts.Total >= thresholds.WarningCount) warnings.Add("queue_count_threshold");
        if (age >= thresholds.WarningAgeMinutes * 60L) warnings.Add("oldest_age_threshold");
        if (counts.Expired > 0) warnings.Add("expired_pending");
        if (counts.Exhausted > 0) warnings.Add("attempts_exhausted");
        return new(1, warnings.Count == 0 ? "ok" : "attention", now, counts, oldest, age,
            nextAttempt, nextReservationExpiry, attemptLimit, thresholds, warnings.ToArray());
    }
}

public sealed class EmailQueueStatusReader(IConfiguration configuration, TimeProvider clock)
{
    public async Task<EmailQueueStatusReport> ReadAsync(EmailQueueThresholds thresholds, CancellationToken cancellationToken)
    {
        thresholds.Validate();
        var attemptLimit = configuration.GetValue("Email:DeliveryAttempts", 3);
        if (attemptLimit is < 1 or > 5) throw new InvalidOperationException("Configuración de intentos no válida.");
        var connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? throw new InvalidOperationException("Falta la conexión local.");
        var settings = new NpgsqlConnectionStringBuilder(connectionString)
        { Timeout = 3, CommandTimeout = 5, ApplicationName = "SecurityCenterAI.EmailQueueStatus" };
        await using var connection = new NpgsqlConnection(settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await using (var guard = new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL lock_timeout = '1000ms'; SET LOCAL statement_timeout = '5000ms'", connection, transaction))
            await guard.ExecuteNonQueryAsync(cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        // Only counters and scheduling metadata are selected. Never read or decrypt payloads.
        const string query = """
            WITH queue_state AS (
              SELECT fecha_creacion_utc, fecha_proximo_intento_utc, fecha_fin_reserva_utc, intentos,
                CASE
                  WHEN id_reserva IS NOT NULL AND fecha_fin_reserva_utc > @now THEN 'reserved'
                  WHEN fecha_expiracion_utc <= @now THEN 'expired'
                  WHEN intentos >= @attempt_limit THEN 'exhausted'
                  WHEN fecha_proximo_intento_utc <= @now THEN 'ready'
                  ELSE 'waiting'
                END AS state
              FROM public.account_email_outbox
            )
            SELECT count(*),
              count(*) FILTER (WHERE state = 'ready'),
              count(*) FILTER (WHERE state = 'waiting'),
              count(*) FILTER (WHERE state = 'reserved'),
              count(*) FILTER (WHERE state = 'expired'),
              count(*) FILTER (WHERE state = 'exhausted'),
              count(*) FILTER (WHERE state IN ('ready','waiting') AND intentos > 0),
              min(fecha_creacion_utc),
              min(fecha_proximo_intento_utc) FILTER (WHERE state IN ('ready','waiting')),
              min(fecha_fin_reserva_utc) FILTER (WHERE state = 'reserved')
            FROM queue_state
            """;
        EmailQueueStatusReport report;
        await using (var command = new NpgsqlCommand(query, connection, transaction))
        {
            command.Parameters.AddWithValue("now", now);
            command.Parameters.AddWithValue("attempt_limit", attemptLimit);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("No se obtuvo el resumen de cola.");
            var counts = new EmailQueueCounts(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3),
                reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6));
            DateTime? Date(int index) => reader.IsDBNull(index) ? null : reader.GetDateTime(index);
            report = EmailQueueStatusReport.Create(now, counts, Date(7), Date(8), Date(9), attemptLimit, thresholds);
        }
        await transaction.CommitAsync(cancellationToken);
        return report;
    }
}
