using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecurityCenterAI.Api.Configuration;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services;

public sealed class AccountSessionService(AppDbContext database, TimeProvider clock, IOptions<AuthSecurityOptions> security)
{
    public const int PageLimit = 50;
    public const int MaxPage = 10_000;

    public async Task<AccountSessionListResponse> ListAsync(Guid userId, Guid currentSessionId, int page, CancellationToken cancellationToken)
    {
        if (page is < 1 or > MaxPage) throw new ArgumentOutOfRangeException(nameof(page));
        var now = clock.GetUtcNow().UtcDateTime;
        var sessions = await database.UserSessions.AsNoTracking()
            .Where(item => item.UserId == userId && item.RevokedAtUtc == null && item.ExpiresAtUtc > now)
            .OrderByDescending(item => item.Id == currentSessionId)
            .ThenByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id)
            .Skip((page - 1) * PageLimit).Take(PageLimit + 1)
            .Select(item => new AccountSessionResponse(item.Id, item.CreatedAtUtc, item.ExpiresAtUtc, item.Id == currentSessionId))
            .ToListAsync(cancellationToken);
        return new(sessions.Take(PageLimit).ToArray(), page, PageLimit, sessions.Count > PageLimit, security.Value.MaxActiveSessions);
    }
}
