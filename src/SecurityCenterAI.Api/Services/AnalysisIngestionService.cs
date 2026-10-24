using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Domain.Rules;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services;

public sealed class AnalysisIngestionService(AppDbContext database, TimeProvider clock)
{
    private const int ScoringVersion = 1;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static DeviceOperationException Invalid() => new(400, "El informe no cumple el formato admitido. Ejecutá un nuevo análisis con el agente actualizado.");

    public async Task<AnalysisReceipt> SubmitAsync(Guid deviceId, Guid credentialId,
        SubmitAnalysisRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        // Only semantic upload fields enter this canonical hash, ordered independently
        // of JSON formatting and check order. Credentials and server decisions never do.
        var canonical = request with { Checks = request.Checks.OrderBy(check => check.RuleId, StringComparer.Ordinal).ToArray() };
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical)));

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await using var transaction = database.Database.IsRelational()
                ? await database.Database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
                var credential = await database.DeviceCredentials.Include(item => item.Device).ThenInclude(item => item.User)
                    .SingleOrDefaultAsync(item => item.Id == credentialId && item.DeviceId == deviceId, cancellationToken);
                if (credential is null || credential.RevokedAtUtc.HasValue || credential.ExpiresAtUtc <= Now || credential.Device.RevokedAtUtc.HasValue)
                    throw new DeviceOperationException(401, "La credencial del dispositivo ya no está disponible. Volvé a vincular el equipo.");
                if (await database.DeletedAnalysisReports.AsNoTracking().AnyAsync(item => item.DeviceId == deviceId &&
                    item.ReportId == request.ReportId, cancellationToken))
                    throw new DeviceOperationException(410, "El informe fue eliminado. Ejecutá un nuevo análisis con el agente.");
                var existing = await database.SecurityAnalyses.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.DeviceId == deviceId && item.ReportId == request.ReportId, cancellationToken);
                if (existing is not null)
                {
                    if (!string.Equals(existing.ReportHash, hash, StringComparison.Ordinal))
                        throw new DeviceOperationException(409, "El identificador del informe ya fue usado con otros datos.");
                    return Receipt(existing, true);
                }

                var device = credential.Device;
                var now = Now;
                var analysis = Evaluate(request, now, device);
                analysis.ReportHash = hash;
                database.SecurityAnalyses.Add(analysis);
                device.LastSeenAtUtc = now;
                device.AgentVersion = request.AgentVersion;
                device.Platform = "Windows";
                device.ConcurrencyStamp = Guid.NewGuid();
                credential.ConcurrencyStamp = Guid.NewGuid();
                // A reset, revocation or re-pair must serialize with acceptance. These
                // optimistic guards are written in the same transaction as the report.
                device.User.ConcurrencyStamp = Guid.NewGuid();
                await database.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return Receipt(analysis, false);
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                database.ChangeTracker.Clear();
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName:
                    "FK_security_analyses_users_UserId" or "FK_security_analyses_devices_id_dispositivo" })
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                database.ChangeTracker.Clear();
                throw new DeviceOperationException(401, "La credencial del dispositivo ya no está disponible. Volvé a vincular el equipo.");
            }
        }
        throw new DeviceOperationException(409, "Otra operación modificó el dispositivo. Volvé a intentar enviar el mismo informe.");
    }

    private static bool IsRetryable(Exception exception) => exception is DbUpdateConcurrencyException ||
        exception is InvalidOperationException { InnerException: { } inner } && IsRetryable(inner) ||
        exception is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } ||
        exception is DbUpdateException { InnerException: PostgresException postgres } &&
        (postgres.SqlState is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure ||
            postgres is { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UX_security_analyses_dispositivo_informe" });

    private void Validate(SubmitAnalysisRequest request)
    {
        var now = clock.GetUtcNow();
        if (request.ReportId == Guid.Empty || request.SchemaVersion != 2 || string.IsNullOrWhiteSpace(request.AgentVersion) ||
            request.AgentVersion.Length > 64 || request.AgentVersion.Any(char.IsControl) ||
            request.StartedAtUtc.Offset != TimeSpan.Zero || request.CompletedAtUtc.Offset != TimeSpan.Zero ||
            request.StartedAtUtc > request.CompletedAtUtc || request.CompletedAtUtc - request.StartedAtUtc > TimeSpan.FromMinutes(30) ||
            request.StartedAtUtc < now.AddDays(-7) || request.CompletedAtUtc > now.AddMinutes(5) ||
            request.Checks is null || request.Checks.Count != WindowsRuleCatalog.Rules.Count) throw Invalid();
        var expected = WindowsRuleCatalog.Rules.Select(rule => rule.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var check in request.Checks)
        {
            if (check is null || check.RuleId is null || !expected.Remove(check.RuleId) || check.RuleVersion != WindowsRuleCatalog.Version ||
                check.Status is not ("passed" or "attention" or "unknown" or "notApplicable") ||
                string.IsNullOrWhiteSpace(check.Evidence) || check.Evidence.Length > 4000 ||
                check.Evidence.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')) ||
                check.ObservedAtUtc.Offset != TimeSpan.Zero || check.ObservedAtUtc < request.StartedAtUtc.AddSeconds(-5) ||
                check.ObservedAtUtc > request.CompletedAtUtc.AddSeconds(5)) throw Invalid();
        }
    }

    private static SecurityAnalysis Evaluate(SubmitAnalysisRequest request, DateTime receivedAt, Device device)
    {
        var byId = request.Checks.ToDictionary(check => check.RuleId, StringComparer.Ordinal);
        var passed = request.Checks.Count(check => check.Status == "passed");
        var attention = request.Checks.Count(check => check.Status == "attention");
        var unknown = request.Checks.Count(check => check.Status == "unknown");
        var notApplicable = request.Checks.Count(check => check.Status == "notApplicable");
        var applicable = request.Checks.Count - notApplicable;
        var denominator = WindowsRuleCatalog.Rules.Where(rule => byId[rule.Id].Status != "notApplicable").Sum(rule => rule.Weight);
        var earned = WindowsRuleCatalog.Rules.Where(rule => byId[rule.Id].Status == "passed").Sum(rule => rule.Weight);
        var analysis = new SecurityAnalysis
        {
            UserId = device.UserId, DeviceId = device.Id, ReportId = request.ReportId, SchemaVersion = request.SchemaVersion,
            Source = "windows-agent", AgentVersion = request.AgentVersion, StartedAtUtc = request.StartedAtUtc.UtcDateTime,
            CompletedAtUtc = request.CompletedAtUtc.UtcDateTime, AnalyzedAtUtc = receivedAt, ScoringVersion = ScoringVersion,
            Score = passed + attention == 0 || denominator == 0 ? null : 100 * earned / denominator,
            CoveragePercent = applicable == 0 ? 0 : 100 * (passed + attention) / applicable,
            PassedCount = passed, AttentionCount = attention, UnknownCount = unknown, NotApplicableCount = notApplicable
        };
        foreach (var rule in WindowsRuleCatalog.Rules)
        {
            var check = byId[rule.Id];
            analysis.Findings.Add(new SecurityFinding
            {
                Analysis = analysis, SecurityAnalysisId = analysis.Id, RuleId = rule.Id, RuleVersion = WindowsRuleCatalog.Version,
                Title = rule.Title, Severity = check.Status == "attention" ? rule.Severity : "informational", Status = check.Status,
                Evidence = check.Evidence, Recommendation = rule.Recommendation, DetectedAtUtc = check.ObservedAtUtc.UtcDateTime
            });
        }
        return analysis;
    }

    private static AnalysisReceipt Receipt(SecurityAnalysis analysis, bool alreadyReceived) => new(analysis.Id,
        analysis.ReportId!.Value, analysis.Score, analysis.CoveragePercent!.Value, analysis.PassedCount!.Value,
        analysis.AttentionCount!.Value, analysis.UnknownCount!.Value, analysis.NotApplicableCount!.Value,
        analysis.ScoringVersion!.Value, analysis.AnalyzedAtUtc, alreadyReceived);
}
