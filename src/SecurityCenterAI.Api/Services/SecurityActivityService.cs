using System.Collections.Frozen;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Services;

public static class SecurityActivityRecorder
{
    private static readonly FrozenDictionary<string, SecurityActivityKind> KindsByAction =
        Enum.GetValues<SecurityActivityKind>().ToFrozenDictionary(ActionCode, kind => kind, StringComparer.Ordinal);

    public static bool TryParseAction(string action, out SecurityActivityKind kind) => KindsByAction.TryGetValue(action, out kind);

    // This only tracks an entry. The business operation's SaveChanges/transaction
    // commits both together; retries discard the tracker before recording again.
    public static void Add(AppDbContext database, Guid userId, SecurityActivityKind kind, DateTime occurredAtUtc) =>
        database.SecurityActivities.Add(new() { UserId = userId, Kind = kind, OccurredAtUtc = occurredAtUtc });

    public static SecurityActivityResponse ToResponse(SecurityActivity item) => new(item.Id, ActionCode(item.Kind), item.OccurredAtUtc);

    private static string ActionCode(SecurityActivityKind kind) => kind switch
    {
        SecurityActivityKind.AccountRegistered => "account.registered",
        SecurityActivityKind.SessionStarted => "session.started",
        SecurityActivityKind.SessionRevoked => "session.revoked",
        SecurityActivityKind.AllSessionsRevoked => "sessions.revoked",
        SecurityActivityKind.RefreshReplayBlocked => "session.replay-blocked",
        SecurityActivityKind.EmailVerified => "email.verified",
        SecurityActivityKind.EmailChanged => "email.changed",
        SecurityActivityKind.PasswordReset => "password.reset",
        SecurityActivityKind.PasswordChanged => "password.changed",
        SecurityActivityKind.DeviceRegistered => "device.registered",
        SecurityActivityKind.PairingCodeIssued => "device.code-issued",
        SecurityActivityKind.DevicePaired => "device.paired",
        SecurityActivityKind.DeviceRevoked => "device.revoked",
        SecurityActivityKind.AnalysisDeleted => "analysis.deleted",
        _ => throw new InvalidOperationException("Tipo de actividad no reconocido.")
    };
}

public sealed class SecurityActivityService(AppDbContext database)
{
    public const int PageLimit = 50;

    public async Task<SecurityActivityListResponse> ListAsync(Guid userId, SecurityActivityQuery filter, CancellationToken cancellationToken)
    {
        var from = ParseDate(filter.From);
        var to = ParseDate(filter.To);
        if (from > to || to == DateTime.SpecifyKind(DateTime.MaxValue.Date, DateTimeKind.Utc)) throw InvalidQuery();
        var query = database.SecurityActivities.AsNoTracking().Where(item => item.UserId == userId);
        if (!string.IsNullOrEmpty(filter.Action))
        {
            if (!SecurityActivityRecorder.TryParseAction(filter.Action, out var kind)) throw InvalidQuery();
            query = query.Where(item => item.Kind == kind);
        }
        if (from.HasValue) query = query.Where(item => item.OccurredAtUtc >= from.Value);
        if (to.HasValue)
        {
            var until = to.Value.AddDays(1);
            query = query.Where(item => item.OccurredAtUtc < until);
        }
        if (!string.IsNullOrEmpty(filter.Before))
        {
            if (filter.Before.Length != 36 || !Guid.TryParseExact(filter.Before, "D", out var id) || id == Guid.Empty) throw InvalidQuery();
            // Resolve the anchor inside the owner's filtered set. A foreign/missing
            // event has the same error and can never authorize another account.
            var anchor = await query.Where(item => item.Id == id)
                .Select(item => new { item.Id, item.OccurredAtUtc }).SingleOrDefaultAsync(cancellationToken);
            if (anchor is null) throw InvalidQuery();
            query = query.Where(item => item.OccurredAtUtc < anchor.OccurredAtUtc ||
                item.OccurredAtUtc == anchor.OccurredAtUtc && item.Id.CompareTo(anchor.Id) < 0);
        }
        var rows = await query
            .OrderByDescending(item => item.OccurredAtUtc).ThenByDescending(item => item.Id)
            .Take(PageLimit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > PageLimit;
        return new(rows.Take(PageLimit).Select(SecurityActivityRecorder.ToResponse).ToArray(), hasMore, PageLimit,
            hasMore ? rows[PageLimit - 1].Id : null);
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value.Length != 10 || !DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)) throw InvalidQuery();
        return date;
    }

    private static DeviceOperationException InvalidQuery() => new(400,
        "Revisá la acción, las fechas UTC y el punto de continuación. Volvé a los eventos más recientes si el enlace ya no está disponible.");
}
