using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Security;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var profile = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new ProfileResponse(
                user.Id,
                user.Name,
                user.Email,
                user.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        return profile is null ? Unauthorized() : Ok(profile);
    }
}
