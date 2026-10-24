using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Route("api/account/security-activity")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("DeviceAccount")]
public sealed class SecurityActivityController(SecurityActivityService activity) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<SecurityActivityListResponse>> Get([FromQuery] SecurityActivityQuery query, CancellationToken cancellationToken)
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (new[] { "action", "from", "to", "before" }.Any(key => Request.Query.TryGetValue(key, out var values) && values.Count != 1))
            return Problem(statusCode: 400, title: "Usá un solo valor por filtro de actividad.");
        try { return await activity.ListAsync(Guid.Parse(User.FindFirstValue("sub")!), query, cancellationToken); }
        catch (DeviceOperationException exception) { return Problem(statusCode: exception.StatusCode, title: exception.Message); }
    }
}
