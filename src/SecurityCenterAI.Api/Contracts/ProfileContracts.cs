namespace SecurityCenterAI.Api.Contracts;

public sealed record ProfileResponse(
    Guid Id,
    string Name,
    string Email,
    DateTime CreatedAtUtc);
