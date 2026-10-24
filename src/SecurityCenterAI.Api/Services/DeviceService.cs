using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services;

public sealed class DeviceOperationException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class DeviceService(AppDbContext database, TimeProvider clock, IOptions<DeviceSecurityOptions> options)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static DeviceOperationException Missing() => new(404, "El dispositivo no está disponible.");
    private static DeviceOperationException InvalidCode() => new(400, "El código no es válido o ya venció. Solicitá uno nuevo desde tu cuenta.");

    public async Task<IReadOnlyList<DeviceResponse>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var devices = await database.Devices.AsNoTracking().Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id).Take(100).ToListAsync(cancellationToken);
        var ids = devices.Select(item => item.Id).ToArray();
        var credentials = await database.DeviceCredentials.AsNoTracking().Where(item => ids.Contains(item.DeviceId) &&
            item.RevokedAtUtc == null && item.ExpiresAtUtc > Now).ToListAsync(cancellationToken);
        return devices.Select(device => Profile(device, credentials.FirstOrDefault(item => item.DeviceId == device.Id))).ToList();
    }

    public async Task<DeviceDetailResponse> DetailAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken)
    {
        var device = await database.Devices.AsNoTracking().SingleOrDefaultAsync(item => item.Id == deviceId && item.UserId == userId, cancellationToken)
            ?? throw Missing();
        var analyses = await database.SecurityAnalyses.AsNoTracking().Include(item => item.Findings
                .OrderByDescending(finding => finding.DetectedAtUtc).ThenByDescending(finding => finding.Id).Take(100))
            .Where(item => item.DeviceId == deviceId && item.UserId == userId)
            .OrderByDescending(item => item.AnalyzedAtUtc).ThenByDescending(item => item.Id).Take(20).ToListAsync(cancellationToken);
        return new DeviceDetailResponse(await ProfileAsync(device, cancellationToken), analyses.Select(ToAnalysisResponse).ToList());
    }

    internal static DeviceAnalysisResponse ToAnalysisResponse(SecurityAnalysis analysis) =>
        new(analysis.Id, analysis.Score, analysis.AnalyzedAtUtc, analysis.Findings
            .OrderBy(finding => finding.RuleId).ThenBy(finding => finding.Id).Select(finding => new SecurityFindingResponse(
                finding.Id, finding.RuleId, finding.RuleVersion, finding.Title, finding.Severity,
                finding.Evidence, finding.Recommendation, finding.DetectedAtUtc, finding.Status)).ToList(),
            analysis.Source, analysis.ScoringVersion, analysis.CoveragePercent, analysis.PassedCount,
            analysis.AttentionCount, analysis.UnknownCount, analysis.NotApplicableCount, analysis.AgentVersion,
            analysis.StartedAtUtc, analysis.CompletedAtUtc, analysis.ReportId);

    public async Task<DevicePairingResponse> CreateAsync(Guid userId, string name, CancellationToken cancellationToken)
    {
        name = name.Trim();
        if (name.Length is < 2 or > 100 || name.Any(char.IsControl))
            throw new DeviceOperationException(400, "Usá un nombre de entre 2 y 100 caracteres.");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var user = await VerifiedUserAsync(userId, cancellationToken);
            if (await database.Devices.AnyAsync(item => item.UserId == userId && item.RevokedAtUtc == null, cancellationToken))
                throw new DeviceOperationException(409, "Tu cuenta ya tiene un dispositivo activo o pendiente de vinculación.");
            user.ConcurrencyStamp = Guid.NewGuid();
            var device = new Device { User = user, UserId = userId, Name = name, CreatedAtUtc = Now };
            database.Devices.Add(device);
            var (code, expires) = AddCode(device);
            SecurityActivityRecorder.Add(database, userId, SecurityActivityKind.DeviceRegistered, Now);
            try
            {
                await database.SaveChangesAsync(cancellationToken);
                return new DevicePairingResponse(Profile(device, null), code, expires);
            }
            catch (Exception exception) when (ConcurrentWrite(exception)) { database.ChangeTracker.Clear(); }
            catch (DbUpdateException exception) when (AuditOwnerRemoved(exception)) { database.ChangeTracker.Clear(); throw Missing(); }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UX_devices_usuario_activo" })
            {
                database.ChangeTracker.Clear();
                throw new DeviceOperationException(409, "Tu cuenta ya tiene un dispositivo activo o pendiente de vinculación.");
            }
        }
        throw new DeviceOperationException(409, "Otra operación modificó tu cuenta. Volvé a intentar.");
    }

    public async Task<DevicePairingResponse> ReissueAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            // Check ownership before any operation-specific response.
            var device = await OwnedAsync(userId, deviceId, cancellationToken);
            var user = await VerifiedUserAsync(userId, cancellationToken);
            if (device.RevokedAtUtc.HasValue) throw new DeviceOperationException(409, "Un dispositivo revocado no puede volver a vincularse. Creá uno nuevo.");
            await RevokeCodesAsync(deviceId, cancellationToken);
            device.ConcurrencyStamp = Guid.NewGuid();
            user.ConcurrencyStamp = Guid.NewGuid();
            var (code, expires) = AddCode(device);
            SecurityActivityRecorder.Add(database, userId, SecurityActivityKind.PairingCodeIssued, Now);
            try
            {
                await database.SaveChangesAsync(cancellationToken);
                return new DevicePairingResponse(await ProfileAsync(device, cancellationToken), code, expires);
            }
            catch (Exception exception) when (ConcurrentWrite(exception)) { database.ChangeTracker.Clear(); }
            catch (DbUpdateException exception) when (AuditOwnerRemoved(exception)) { database.ChangeTracker.Clear(); throw Missing(); }
        }
        throw new DeviceOperationException(409, "Otra operación modificó el dispositivo. Volvé a intentar.");
    }

    public async Task RevokeAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var device = await OwnedAsync(userId, deviceId, cancellationToken);
            if (device.RevokedAtUtc.HasValue) return;
            device.RevokedAtUtc = Now;
            device.ConcurrencyStamp = Guid.NewGuid();
            await RevokeCodesAsync(deviceId, cancellationToken);
            await RevokeCredentialsAsync(deviceId, cancellationToken);
            SecurityActivityRecorder.Add(database, userId, SecurityActivityKind.DeviceRevoked, Now);
            try { await database.SaveChangesAsync(cancellationToken); return; }
            catch (Exception exception) when (attempt < 4 && ConcurrentWrite(exception)) { database.ChangeTracker.Clear(); }
            catch (DbUpdateException exception) when (AuditOwnerRemoved(exception)) { database.ChangeTracker.Clear(); throw Missing(); }
        }
    }

    public async Task<DevicePairResponse> PairAsync(string code, string agentVersion, CancellationToken cancellationToken)
    {
        if (!OpaqueToken.IsValid(code)) throw InvalidCode();
        agentVersion = ValidateVersion(agentVersion);
        var hash = OpaqueToken.Hash(code);
        // Two saves enforce the partial unique active-credential index. Both belong to
        // one PostgreSQL transaction, so consumers never observe a half-finished pairing.
        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            var pairing = await database.DevicePairingCodes.Include(item => item.Device).ThenInclude(item => item.User)
                .SingleOrDefaultAsync(item => item.CodeHash == hash, cancellationToken);
            if (pairing is null || pairing.ConsumedAtUtc.HasValue || pairing.RevokedAtUtc.HasValue || pairing.ExpiresAtUtc <= Now ||
                pairing.Device.RevokedAtUtc.HasValue || !pairing.Device.User.EmailVerified) throw InvalidCode();
            var device = pairing.Device;
            pairing.ConsumedAtUtc = Now;
            pairing.ConcurrencyStamp = Guid.NewGuid();
            device.PairedAtUtc = Now;
            device.AgentVersion = agentVersion;
            device.ConcurrencyStamp = Guid.NewGuid();
            // Reset reads credentials/codes separately; advance the user version so it
            // cannot commit a stale revocation snapshot after this transaction.
            device.User.ConcurrencyStamp = Guid.NewGuid();
            await RevokeCredentialsAsync(device.Id, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            var token = OpaqueToken.Create();
            var expires = Now.AddDays(options.Value.CredentialDays);
            database.DeviceCredentials.Add(new DeviceCredential
            { Device = device, DeviceId = device.Id, TokenHash = OpaqueToken.Hash(token), CreatedAtUtc = Now, ExpiresAtUtc = expires });
            SecurityActivityRecorder.Add(database, device.UserId, SecurityActivityKind.DevicePaired, Now);
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new DevicePairResponse(device.Id, token, expires);
        }
        catch (Exception exception) when (ConcurrentWrite(exception) || exception is DbUpdateException update && AuditOwnerRemoved(update))
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            throw InvalidCode();
        }
    }

    public async Task<bool> HeartbeatAsync(Guid deviceId, Guid credentialId, string agentVersion, CancellationToken cancellationToken)
    {
        agentVersion = ValidateVersion(agentVersion);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var credential = await database.DeviceCredentials.Include(item => item.Device).SingleOrDefaultAsync(item =>
                item.Id == credentialId && item.DeviceId == deviceId, cancellationToken);
            if (credential is null || credential.RevokedAtUtc.HasValue || credential.ExpiresAtUtc <= Now || credential.Device.RevokedAtUtc.HasValue) return false;
            credential.Device.LastSeenAtUtc = Now;
            credential.Device.AgentVersion = agentVersion;
            credential.Device.ConcurrencyStamp = Guid.NewGuid();
            database.Entry(credential).Property(item => item.ConcurrencyStamp).IsModified = true;
            try { await database.SaveChangesAsync(cancellationToken); return true; }
            catch (DbUpdateConcurrencyException) { database.ChangeTracker.Clear(); }
        }
        return false;
    }

    private async Task<User> VerifiedUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await database.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken) ?? throw Missing();
        if (!user.EmailVerified) throw new DeviceOperationException(403, "Confirmá tu correo antes de vincular un dispositivo.");
        return user;
    }

    private async Task<Device> OwnedAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken) =>
        await database.Devices.SingleOrDefaultAsync(item => item.Id == deviceId && item.UserId == userId, cancellationToken) ?? throw Missing();

    private (string Code, DateTime Expires) AddCode(Device device)
    {
        var code = OpaqueToken.Create();
        var expires = Now.AddMinutes(options.Value.PairingMinutes);
        database.DevicePairingCodes.Add(new DevicePairingCode
        { Device = device, DeviceId = device.Id, CodeHash = OpaqueToken.Hash(code), CreatedAtUtc = Now, ExpiresAtUtc = expires });
        return (code, expires);
    }

    private async Task RevokeCodesAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var codes = await database.DevicePairingCodes.Where(item => item.DeviceId == deviceId && item.ConsumedAtUtc == null && item.RevokedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var code in codes) { code.RevokedAtUtc = Now; code.ConcurrencyStamp = Guid.NewGuid(); }
    }

    private async Task RevokeCredentialsAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var credentials = await database.DeviceCredentials.Where(item => item.DeviceId == deviceId && item.RevokedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var credential in credentials) { credential.RevokedAtUtc = Now; credential.ConcurrencyStamp = Guid.NewGuid(); }
    }

    private async Task<DeviceResponse> ProfileAsync(Device device, CancellationToken cancellationToken)
    {
        var credential = await database.DeviceCredentials.AsNoTracking().SingleOrDefaultAsync(item => item.DeviceId == device.Id &&
            item.RevokedAtUtc == null && item.ExpiresAtUtc > Now, cancellationToken);
        return Profile(device, credential);
    }

    private static DeviceResponse Profile(Device device, DeviceCredential? credential) => ToDeviceResponse(device, credential?.ExpiresAtUtc);

    internal static DeviceResponse ToDeviceResponse(Device device, DateTime? validCredentialExpiresAtUtc) => new(device.Id, device.Name,
        device.CreatedAtUtc, device.PairedAtUtc, device.LastSeenAtUtc, device.RevokedAtUtc, device.Platform, device.AgentVersion,
        device.RevokedAtUtc.HasValue ? "revoked" : validCredentialExpiresAtUtc is null ? "pending" : "linked",
        device.RevokedAtUtc.HasValue ? null : validCredentialExpiresAtUtc);

    private static string ValidateVersion(string version)
    {
        version = version.Trim();
        if (version.Length is < 1 or > 64 || version.Any(char.IsControl)) throw new DeviceOperationException(400, "La versión del agente no es válida.");
        return version;
    }

    private static bool AuditOwnerRemoved(DbUpdateException exception) => exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName: "FK_security_activity_users_id_usuario" };

    private static bool ConcurrentWrite(Exception exception) => exception is DbUpdateConcurrencyException ||
        exception is InvalidOperationException { InnerException: { } inner } && ConcurrentWrite(inner) ||
        exception is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } ||
        exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } };
}
