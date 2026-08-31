using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Domain.Entities;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AppDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    ITokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("registration")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedName = request.Name.Trim();

        if (normalizedName.Length < 2)
        {
            ModelState.AddModelError(nameof(request.Name), "El nombre debe tener al menos 2 caracteres.");
            return ValidationProblem(ModelState);
        }

        if (await dbContext.Users.AnyAsync(
                user => user.Email == normalizedEmail,
                cancellationToken))
        {
            return EmailAlreadyRegistered();
        }

        var user = new User
        {
            Name = normalizedName,
            Email = normalizedEmail,
            PasswordHash = string.Empty
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsEmailUniqueViolation(exception))
        {
            dbContext.Entry(user).State = EntityState.Detached;
            return EmailAlreadyRegistered();
        }

        var response = CreateResponse(user);
        return Created("/api/profile", response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Email == normalizedEmail,
            cancellationToken);

        if (user is null)
        {
            return InvalidCredentials();
        }

        var verification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(CreateResponse(user));
    }

    private AuthResponse CreateResponse(User user)
    {
        var (token, expiresAt) = tokenService.Create(user);
        return new AuthResponse(
            token,
            expiresAt,
            new UserResponse(user.Id, user.Name, user.Email));
    }

    private static ConflictObjectResult EmailAlreadyRegistered()
    {
        return new ConflictObjectResult(new ProblemDetails
        {
            Title = "El correo ya está registrado.",
            Status = StatusCodes.Status409Conflict
        });
    }

    private static UnauthorizedObjectResult InvalidCredentials()
    {
        return new UnauthorizedObjectResult(new ProblemDetails
        {
            Title = "Credenciales inválidas.",
            Status = StatusCodes.Status401Unauthorized
        });
    }

    private static bool IsEmailUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_users_Email"
        };
    }
}
