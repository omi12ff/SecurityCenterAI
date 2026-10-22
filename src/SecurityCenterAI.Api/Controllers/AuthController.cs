using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("AuthPublic")]
public sealed class AuthController(AccountService accounts, AppDbContext database) : ControllerBase
{
    private const string GenericEmailMessage = "Si corresponde, recibirás un correo con los próximos pasos. Revisá también la carpeta de correo no deseado.";
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (request.Name.Trim().Length < 2) return BadRequest(new ProblemDetails { Title = "Revisá los datos ingresados.", Status = 400 });
        var result = await accounts.RegisterAsync(request, cancellationToken);
        return result is null ? Conflict(new ProblemDetails { Title = "El correo ya está registrado.", Status = 409 }) : CreatedAtAction(nameof(Register), result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await accounts.LoginAsync(request, cancellationToken);
        return result is null ? InvalidCredentials() : Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await accounts.RefreshAsync(request.RefreshToken, cancellationToken);
        return result is null ? InvalidCredentials() : Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken cancellationToken)
    {
        await accounts.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        await accounts.LogoutAllAsync(UserId, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == UserId, cancellationToken);
        return user is null ? Unauthorized() : Ok(AccountService.Profile(user));
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("AccountEmail")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await accounts.ForgotPasswordAsync(request.Email, cancellationToken);
        return Ok(new { message = GenericEmailMessage });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken) =>
        await accounts.ResetPasswordAsync(request.Token, request.Password, cancellationToken) ? NoContent() : InvalidActionToken();

    [Authorize]
    [HttpPost("resend-verification")]
    [EnableRateLimiting("AccountEmail")]
    public async Task<IActionResult> ResendVerification(CancellationToken cancellationToken)
    {
        await accounts.ResendVerificationAsync(UserId, cancellationToken);
        return Ok(new { message = GenericEmailMessage });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken cancellationToken) =>
        await accounts.VerifyEmailAsync(request.Token, cancellationToken) ? NoContent() : InvalidActionToken();

    private UnauthorizedObjectResult InvalidCredentials() => Unauthorized(new ProblemDetails { Title = "Credenciales inválidas.", Status = 401 });
    private BadRequestObjectResult InvalidActionToken() => BadRequest(new ProblemDetails { Title = "El enlace no es válido o ya venció. Solicitá uno nuevo.", Status = 400 });
}
