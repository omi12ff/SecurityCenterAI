using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Route("api/account/password")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("AccountPassword")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountPasswordController(AccountPasswordService passwords) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> Change(ChangeAccountPasswordRequest request, CancellationToken cancellationToken)
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        try
        {
            await passwords.ChangeAsync(Guid.Parse(User.FindFirstValue("sub")!), Guid.Parse(User.FindFirstValue("sid")!), request, cancellationToken);
            return NoContent();
        }
        catch (NewPasswordRejectedException)
        { return BadRequest(new { code = "password_policy", title = NewPasswordPolicy.Message, status = 400 }); }
        catch (DeviceOperationException exception)
        { return StatusCode(exception.StatusCode, new ProblemDetails { Status = exception.StatusCode, Title = exception.Message }); }
    }
}
