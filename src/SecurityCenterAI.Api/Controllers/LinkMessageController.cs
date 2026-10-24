using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;

namespace SecurityCenterAI.Api.Controllers;

[ApiController, Route("api/tools/link-message")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("DeviceAccount")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LinkMessageController(LinkMessageService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Ok(await service.StatusAsync(Guid.Parse(User.FindFirstValue("sub")!), cancellationToken));
    }

    [HttpPost, RequestSizeLimit(32768)]
    public async Task<IActionResult> Check(LinkMessageRequest request, CancellationToken cancellationToken)
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        try { return Ok(await service.CheckAsync(Guid.Parse(User.FindFirstValue("sub")!), Guid.Parse(User.FindFirstValue("sid")!), request, cancellationToken)); }
        catch (DeviceOperationException exception) { return Problem(statusCode: exception.StatusCode, title: exception.Message); }
    }
}
