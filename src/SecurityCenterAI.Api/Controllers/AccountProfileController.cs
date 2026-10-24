using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Route("api/account/profile")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("DeviceAccount")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountProfileController(AccountProfileService profiles) : ControllerBase
{
    [HttpPost("name")]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> UpdateName(UpdateProfileNameRequest request, CancellationToken cancellationToken)
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        try
        {
            await profiles.UpdateNameAsync(Guid.Parse(User.FindFirstValue("sub")!), Guid.Parse(User.FindFirstValue("sid")!), request.Name, cancellationToken);
            return NoContent();
        }
        catch (DeviceOperationException exception)
        { return StatusCode(exception.StatusCode, new ProblemDetails { Status = exception.StatusCode, Title = exception.Message }); }
    }
}
