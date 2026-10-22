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
    TimeProvider clock, ILogger<AccountService> logger)
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
        var email = request.Email.Trim().ToLowerInvariant();
        if (await database.Users.AnyAsync(user => user.Email == email, cancellationToken)) return null;
        var user = new User { Name = request.Name.Trim(), Email = email, PasswordHash = "", CreatedAtUtc = Now };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        database.Users.Add(user);
        var verification = await AddActionTokenAsync(user, VerificationPurpose, cancellationToken);
        user.LastVerificationSentAtUtc = Now;
        var response = AddSession(user);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { database.ChangeTracker.Clear(); return null; }
        await DeliverAsync(new AccountEmail(user.Email, verification, AccountEmailPurpose.EmailVerification), cancellationToken);
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
                user.FailedLoginAttempts = 0;
                user.LockoutEndUtc = null;
                if (verification == PasswordVerificationResult.SuccessRehashNeeded) user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
                response = AddSession(user);
            }
            user.ConcurrencyStamp = Guid.NewGuid();
            try { await database.SaveChangesAsync(cancellationToken); return response; }
            catch (DbUpdateConcurrencyException) { database.ChangeTracker.Clear(); }
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
            if (credential.ConsumedAtUtc.HasValue) { await RevokeSessionAsync(session.Id, cancellationToken); return null; }
            if (session.RevokedAtUtc.HasValue || session.ExpiresAtUtc <= Now || credential.ExpiresAtUtc <= Now) return null;
            credential.ConsumedAtUtc = Now;
            credential.ConcurrencyStamp = Guid.NewGuid();
            session.ConcurrencyStamp = Guid.NewGuid();
            // Guard the user version against reset/logout-all without changing it for
            // independent refreshes. SaveChanges is one transaction on PostgreSQL.
            database.Entry(session.User).Property(item => item.ConcurrencyStamp).IsModified = true;
            var response = AddRefreshResponse(session.User, session);
            try { await database.SaveChangesAsync(cancellationToken); return response; }
            catch (DbUpdateConcurrencyException) { database.ChangeTracker.Clear(); }
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
            try { await database.SaveChangesAsync(cancellationToken); return; }
            catch (DbUpdateConcurrencyException) when (attempt < 4) { database.ChangeTracker.Clear(); }
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
            try { await database.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException) { database.ChangeTracker.Clear(); return; }
            await DeliverAsync(new AccountEmail(user.Email, token, AccountEmailPurpose.PasswordReset), cancellationToken);
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
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { database.ChangeTracker.Clear(); return; }
        await DeliverAsync(new AccountEmail(user.Email, token, AccountEmailPurpose.EmailVerification), cancellationToken);
    }

    public async Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken)
    {
        var action = await FindActionAsync(token, VerificationPurpose, cancellationToken);
        if (action is null) return false;
        action.ConsumedAtUtc = Now;
        action.ConcurrencyStamp = Guid.NewGuid();
        action.User.EmailVerified = true;
        action.User.ConcurrencyStamp = Guid.NewGuid();
        try { await database.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateConcurrencyException) { database.ChangeTracker.Clear(); return false; }
    }

    public async Task<bool> ResetPasswordAsync(string token, string password, CancellationToken cancellationToken)
    {
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
        try { await database.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateConcurrencyException) { database.ChangeTracker.Clear(); return false; }
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

    private async Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var session = await database.UserSessions.SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
            if (session is null || session.RevokedAtUtc.HasValue) return;
            session.RevokedAtUtc = Now;
            session.ConcurrencyStamp = Guid.NewGuid();
            try { await database.SaveChangesAsync(cancellationToken); return; }
            catch (DbUpdateConcurrencyException) when (attempt < 4) { database.ChangeTracker.Clear(); }
        }
    }

    private async Task DeliverAsync(AccountEmail message, CancellationToken cancellationToken)
    {
        try { await emailSender.SendAsync(message, cancellationToken); }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        { logger.LogWarning("No se pudo entregar un correo de cuenta. Revisa la disponibilidad del transporte SMTP."); }
    }
}
