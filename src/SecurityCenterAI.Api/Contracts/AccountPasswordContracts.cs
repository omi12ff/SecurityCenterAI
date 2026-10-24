using System.ComponentModel.DataAnnotations;

namespace SecurityCenterAI.Api.Contracts;

public sealed record ChangeAccountPasswordRequest(
    [Required(AllowEmptyStrings = true), MaxLength(128)] string CurrentPassword,
    [Required, StringLength(128, MinimumLength = 15)] string NewPassword,
    bool Confirmed);
