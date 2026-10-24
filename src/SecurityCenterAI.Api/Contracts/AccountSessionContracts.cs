namespace SecurityCenterAI.Api.Contracts;

public sealed record AccountSessionResponse(Guid Id, DateTime CreatedAtUtc, DateTime ExpiresAtUtc, bool IsCurrent);
public sealed record AccountSessionListResponse(IReadOnlyList<AccountSessionResponse> Sessions, int Page, int Limit, bool HasMore, int MaxActiveSessions = 10);
public sealed record RevokeAccountSessionRequest(bool Confirmed);
public sealed record RevokeAccountSessionResponse(bool CurrentSession);
