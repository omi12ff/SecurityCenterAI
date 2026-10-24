using System.ComponentModel.DataAnnotations;

namespace SecurityCenterAI.Api.Contracts;

public sealed record RequestEmailChangeRequest(
    [Required, EmailAddress, MaxLength(320)] string NewEmail,
    [Required(AllowEmptyStrings = true), MaxLength(128)] string CurrentPassword, bool Confirmed);
public sealed record CompleteEmailChangeRequest(Guid RequestId,
    [Required(AllowEmptyStrings = true), MaxLength(128)] string CurrentPassword, bool Confirmed);
public sealed record CancelEmailChangeRequest(Guid RequestId, bool Confirmed);
public sealed record ConfirmEmailChangeRequest([Required, MaxLength(256)] string Token, bool Confirmed);
public sealed record PendingEmailChangeResponse(Guid Id, string NewEmail, DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc, bool CurrentAddressConfirmed, bool NewAddressConfirmed, bool Expired);
public sealed record EmailChangeStatusResponse(PendingEmailChangeResponse? Pending);
