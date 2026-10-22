using System.ComponentModel.DataAnnotations;

namespace SecurityCenterAI.Api.Contracts;

public sealed record RegisterRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    [Required, EmailAddress, MaxLength(320)] string Email,
    [Required, StringLength(128, MinimumLength = 8)] string Password);

public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(320)] string Email,
    [Required, MaxLength(128)] string Password);

public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserResponse User,
    string RefreshToken,
    DateTime RefreshExpiresAtUtc);

public sealed record UserResponse(Guid Id, string Name, string Email, bool EmailVerified);
public sealed record RefreshRequest([Required, StringLength(256, MinimumLength = 1)] string RefreshToken);
public sealed record ForgotPasswordRequest([Required, EmailAddress, MaxLength(320)] string Email);
public sealed record ResetPasswordRequest(
    [Required, MaxLength(256)] string Token,
    [Required, StringLength(128, MinimumLength = 8)] string Password);
public sealed record VerifyEmailRequest([Required, MaxLength(256)] string Token);
