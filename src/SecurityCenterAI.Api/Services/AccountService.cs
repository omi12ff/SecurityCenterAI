using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services;

public sealed class AccountService(AppDbContext database, IPasswordHasher<User> passwordHasher,
    ITokenService tokens, IAccountEmailSender emailSender, IOptions<AuthSecurityOptions> settings,
    TimeProvider clock)
{
    public const string VerificationPurpose = "email-verification";
    public const string PasswordResetPurpose = "password-reset";
    private readonly AuthSecurityOptions _settings = settings.Value;
    private static readonly User DummyUser = new() { Name = "Timing", Email = "unused@example.invalid", PasswordHash = "" };
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(DummyUser, "NotAnAccountPassword123!");
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public static UserResponse Profile(User user) => new(user.Id, user.Name, user.Email, user.EmailVerified);

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        NewPasswordPolicy.Require(request.Password);
        var email = request.Email.Trim().ToLowerInvariant();
        if (await database.Users.AnyAsync(user => user.Email == email, cancellationToken)) return null;
        var user = new User { Name = request.Name.Trim(), Email = email, PasswordHash = "", CreatedAtUtc = Now };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        database.Users.Add(user);
        var verification = await AddActionTokenAsync(user, VerificationPurpose, cancellationToken);
        user.LastVerificationSentAtUtc = Now;
        var response = AddSession(user);
        SecurityActivityRecorder.Add(database, user.Id, SecurityActivityKind.AccountRegistered, Now);
        await emailSender.QueueAsync(user.Id, new(user.Email, verification, AccountEmailPurpose.EmailVerification), cancellationToken);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { database.ChangeTracker.Clear(); return null; }
        return response;
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var user = await database.Users.SingleOrDefaultAsync(item => item.Email == email, cancellationToken);
            if (user is null)
            { passwordHasher.VerifyHashedPassword(DummyUser, DummyHash, request.Password); return null; }
            var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (user.LockoutEndUtc > Now) return null;
            if (user.LockoutEndUtc.HasValue) { user.LockoutEndUtc = null; user.FailedLoginAttempts = 0; }
            AuthResponse? response = null;
            if (verification == PasswordVerificationResult.Failed)
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= _settings.MaxFailedLoginAttempts) user.LockoutEndUtc = Now.AddMinutes(_settings.LockoutMinutes);
            }
            else
            {
                if (await database.UserSessions.CountAsync(item => item.UserId == user.Id &&
                    item.RevokedAtUtc == null && item.ExpiresAtUtc > Now, cancellationToken) >= _settings.MaxActiveSessions)
                    throw new DeviceOperationException(409, "Se alcanzó el límite de sesiones web de la cuenta. Cerrá una sesión o recuperá el acceso por correo.");
                user.FailedLoginAttempts = 0;
                user.LockoutEndUtc = null;
                if (verification == PasswordVerificationResult.SuccessRehashNeeded) user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
                response = AddSession(user);
                SecurityActivityRecorder.Add(database, user.Id, SecurityActivityKind.SessionStarted, Now);
            }
            user.ConcurrencyStamp = Guid.NewGuid();
            try { await database.SaveChangesAsync(cancellationToken); return response; }
            catch (Exception exception) when (ConcurrentWrite(exception)) { database.ChangeTracker.Clear(); }
            catch (DbUpdateException exception) when (OwnerRemoved(exception)) { database.ChangeTracker.Clear(); return null; }
        }
        return null;
    }

    public async Task<AuthResponse?> RefreshAsync(string token, CancellationToken cancellationToken)
    {
        if (!OpaqueToken.IsValid(token)) return null;
        var hash = OpaqueToken.Hash(token);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var credential = await database.RefreshCredentials.Include(item => item.Session).ThenInclude(item => item.User)
                .SingleOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
            if (credential is null) return null;
            var session = credential.Session;
            if (credential.ConsumedAtUtc.HasValue) { await RevokeSessionAsync(session.Id, cancellationToken, SecurityActivityKind.RefreshReplayBlocked); return null; }
            if (session.RevokedAtUtc.HasValue || session.ExpiresAtUtc <= Now || credential.ExpiresAtUtc <= Now) return null;
            credential.ConsumedAtUtc = Now;
            credential.ConcurrencyStamp = Guid.NewGuid();
            session.ConcurrencyStamp = Guid.NewGuid();
            // Guard the user version against reset/logout-all without changing it for
            // independent refreshes. SaveChanges is one transaction on PostgreSQL.
            database.Entry(session.User).Property(item => item.ConcurrencyStamp).IsModified = true;
            var response = AddRefreshResponse(session.User, session);
            try { await database.SaveChangesAsync(cancellationToken); return response; }
            catch (Exception exception) when (ConcurrentWrite(exception)) { database.ChangeTracker.Clear(); }
            catch (DbUpdateException exception) when (OwnerRemoved(exception)) { database.ChangeTracker.Clear(); return null; }
        }
        return null;
    }

    public async Task LogoutAsync(string token, CancellationToken cancellationToken)
    {
        if (!OpaqueToken.IsValid(token)) return;
        var hash = OpaqueToken.Hash(token);
        var id = await database.RefreshCredentials.AsNoTracking().Where(item => item.TokenHash == hash)
            .Select(item => (Guid?)item.UserSessionId).SingleOrDefaultAsync(cancellationToken);
        if (id.HasValue) await RevokeSessionAsync(id.Value, cancellationToken);
    }

    public async Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var user = await database.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
            if (user is null) return;
            user.ConcurrencyStamp = Guid.NewGuid();
            await RevokeAllTrackedAsync(userId, cancellationToken);
            SecurityActivityRecorder.Add(database, userId, SecurityActivityKind.AllSessionsRevoked, Now);
            try { await database.SaveChangesAsync(cancellationToken); return; }
            catch (Exception exception) when (attempt < 4 && ConcurrentWrite(exception)) { database.ChangeTracker.Clear(); }
            catch (DbUpdateException exception) when (OwnerRemoved(exception)) { database.ChangeTracker.Clear(); return; }
        }
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            var normalized = email.Trim().ToLowerInvariant();
            var user = await database.Users.SingleOrDefaultAsync(item => item.Email == normalized, cancellationToken);
            if (user is null || user.LastPasswordResetRequestedAtUtc > Now.AddSeconds(-_settings.EmailCooldownSeconds)) return;
            user.LastPasswordResetRequestedAtUtc = Now;
            user.ConcurrencyStamp = Guid.NewGuid();
            var token = await AddActionTokenAsync(user, PasswordResetPurpose, cancellationToken);
            await emailSender.QueueAsync(user.Id, new(user.Email, token, AccountEmailPurpose.PasswordReset), cancellationToken);
            try { await database.SaveChangesAsync(cancellationToken); }
            catch (Exception exception) when (ConcurrentWrite(exception) || exception is DbUpdateException update && OwnerRemoved(update))
            { database.ChangeTracker.Clear(); return; }
        }
        finally
        {
            // A minimum duration reduces discovery through absent/cooled-down accounts.
            var remaining = TimeSpan.FromMilliseconds(300) - timer.Elapsed;
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, cancellationToken);
        }
    }

    public async Task ResendVerificationAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await database.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || user.EmailVerified || user.LastVerificationSentAtUtc > Now.AddSeconds(-_settings.EmailCooldownSeconds)) return;
        user.LastVerificationSentAtUtc = Now;
        user.ConcurrencyStamp = Guid.NewGuid();
        var token = await AddActionTokenAsync(user, VerificationPurpose, cancellationToken);
        await emailSender.QueueAsync(user.Id, new(user.Email, token, AccountEmailPurpose.EmailVerification), cancellationToken);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (Exception exception) when (ConcurrentWrite(exception) || exception is DbUpdateException update && OwnerRemoved(update))
        { database.ChangeTracker.Clear(); return; }
    }

    public async Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken)
    {
        var action = await FindActionAsync(token, VerificationPurpose, cancellationToken);
        if (action is null) return false;
        action.ConsumedAtUtc = Now;
        action.ConcurrencyStamp = Guid.NewGuid();
        action.User.EmailVerified = true;
        action.User.ConcurrencyStamp = Guid.NewGuid();
        SecurityActivityRecorder.Add(database, action.UserId, SecurityActivityKind.EmailVerified, Now);
        try { await database.SaveChangesAsync(cancellationToken); return true; }
        catch (Exception exception) when (ConcurrentWrite(exception) || exception is DbUpdateException update && OwnerRemoved(update))
        { database.ChangeTracker.Clear(); return false; }
    }

    public async Task<bool> ResetPasswordAsync(string token, string password, CancellationToken cancellationToken)
    {
        NewPasswordPolicy.Require(password);
        var action = await FindActionAsync(token, PasswordResetPurpose, cancellationToken);
        if (action is null) return false;
        action.User.PasswordHash = passwordHasher.HashPassword(action.User, password);
        action.User.FailedLoginAttempts = 0;
        action.User.LockoutEndUtc = null;
        action.User.ConcurrencyStamp = Guid.NewGuid();
        var outstanding = await database.AccountActionTokens.Where(item => item.UserId == action.UserId &&
            item.Purpose == PasswordResetPurpose && item.ConsumedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var item in outstanding) { item.ConsumedAtUtc = Now; item.ConcurrencyStamp = Guid.NewGuid(); }
        await RevokeAllTrackedAsync(action.UserId, cancellationToken);
        var deviceCredentials = await database.DeviceCredentials.Where(item => item.Device.UserId == action.UserId &&
            item.RevokedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var credential in deviceCredentials)
        { credential.RevokedAtUtc = Now; credential.ConcurrencyStamp = Guid.NewGuid(); }
        var pairingCodes = await database.DevicePairingCodes.Where(item => item.Device.UserId == action.UserId &&
            item.RevokedAtUtc == null && item.ConsumedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var code in pairingCodes)
        { code.RevokedAtUtc = Now; code.ConcurrencyStamp = Guid.NewGuid(); }
        var emailChange = await database.EmailChangeRequests.SingleOrDefaultAsync(item => item.UserId == action.UserId, cancellationToken);
        if (emailChange is not null) database.EmailChangeRequests.Remove(emailChange);
        SecurityActivityRecorder.Add(database, action.UserId, SecurityActivityKind.PasswordReset, Now);
        await emailSender.QueueAsync(action.UserId, new(action.User.Email, "", AccountEmailPurpose.PasswordResetCompleted), cancellationToken);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (Exception exception) when (ConcurrentWrite(exception) || exception is DbUpdateException update && OwnerRemoved(update))
        { database.ChangeTracker.Clear(); return false; }
        return true;
    }

    private AuthResponse AddSession(User user)
    {
        var session = new UserSession { User = user, UserId = user.Id, CreatedAtUtc = Now, ExpiresAtUtc = Now.AddDays(_settings.RefreshDays) };
        database.UserSessions.Add(session);
        return AddRefreshResponse(user, session);
    }

    private AuthResponse AddRefreshResponse(User user, UserSession session)
    {
        var refresh = OpaqueToken.Create();
        database.RefreshCredentials.Add(new RefreshCredential { Session = session, UserSessionId = session.Id,
            TokenHash = OpaqueToken.Hash(refresh), CreatedAtUtc = Now, ExpiresAtUtc = session.ExpiresAtUtc });
        var (token, expiresAt) = tokens.Create(user, session.Id);
        return new AuthResponse(token, expiresAt, Profile(user), refresh, session.ExpiresAtUtc);
    }

    private async Task<string> AddActionTokenAsync(User user, string purpose, CancellationToken cancellationToken)
    {
        var outstanding = await database.AccountActionTokens.Where(item => item.UserId == user.Id &&
            item.Purpose == purpose && item.ConsumedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var item in outstanding) { item.ConsumedAtUtc = Now; item.ConcurrencyStamp = Guid.NewGuid(); }
        var token = OpaqueToken.Create();
        database.AccountActionTokens.Add(new AccountActionToken { User = user, UserId = user.Id, Purpose = purpose,
            TokenHash = OpaqueToken.Hash(token), CreatedAtUtc = Now, ExpiresAtUtc = purpose == VerificationPurpose ?
                Now.AddHours(_settings.VerificationHours) : Now.AddMinutes(_settings.PasswordResetMinutes) });
        return token;
    }

    private async Task<AccountActionToken?> FindActionAsync(string token, string purpose, CancellationToken cancellationToken)
    {
        if (!OpaqueToken.IsValid(token)) return null;
        var hash = OpaqueToken.Hash(token);
        var action = await database.AccountActionTokens.Include(item => item.User).SingleOrDefaultAsync(
            item => item.TokenHash == hash && item.Purpose == purpose, cancellationToken);
        return action is null || action.ConsumedAtUtc.HasValue || action.ExpiresAtUtc <= Now ? null : action;
    }

    private async Task RevokeAllTrackedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var sessions = await database.UserSessions.Where(item => item.UserId == userId && item.RevokedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var item in sessions) { item.RevokedAtUtc = Now; item.ConcurrencyStamp = Guid.NewGuid(); }
    }

    public Task<bool> RevokeOwnedSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
        RevokeSessionAsync(sessionId, cancellationToken, ownerId: userId);

    private async Task<bool> RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken,
        SecurityActivityKind kind = SecurityActivityKind.SessionRevoked, Guid? ownerId = null)
    {
        for (var attempt = 0; ; attempt++)
        {
            var session = await database.UserSessions.SingleOrDefaultAsync(item => item.Id == sessionId &&
                (!ownerId.HasValue || item.UserId == ownerId.Value), cancellationToken);
            if (session is null) return false;
            if (session.RevokedAtUtc.HasValue || ownerId.HasValue && session.ExpiresAtUtc <= Now) return true;
            session.RevokedAtUtc = Now;
            session.ConcurrencyStamp = Guid.NewGuid();
            SecurityActivityRecorder.Add(database, session.UserId, kind, Now);
            try { await database.SaveChangesAsync(cancellationToken); return true; }
            catch (Exception exception) when (attempt < 4 && ConcurrentWrite(exception)) { database.ChangeTracker.Clear(); }
            catch (DbUpdateException exception) when (OwnerRemoved(exception)) { database.ChangeTracker.Clear(); return false; }
        }
    }

    private static bool OwnerRemoved(DbUpdateException exception) => exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName:
            "FK_user_sessions_users_UserId" or "FK_refresh_credentials_user_sessions_UserSessionId" or "FK_account_action_tokens_users_UserId" or "FK_security_activity_users_id_usuario" or "FK_account_email_outbox_users_id_usuario" };

    private static bool ConcurrentWrite(Exception exception) => exception is DbUpdateConcurrencyException ||
        exception is InvalidOperationException { InnerException: { } inner } && ConcurrentWrite(inner) ||
        exception is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } ||
        exception is DbUpdateException { InnerException: PostgresException
            { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } };

}
