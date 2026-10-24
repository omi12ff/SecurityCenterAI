using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecurityCenterAI.Api.Services;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Route("api/devices/{deviceId:guid}/analyses/compare")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("DeviceAccount")]
public sealed class AnalysisComparisonController(AnalysisComparisonService comparison) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Compare(Guid deviceId, [FromQuery] Guid baselineId,
        [FromQuery] Guid targetId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await comparison.CompareAsync(Guid.Parse(User.FindFirstValue("sub")!), deviceId,
                baselineId, targetId, cancellationToken));
        }
        catch (DeviceOperationException exception)
        {
            return StatusCode(exception.StatusCode,
                new ProblemDetails { Title = exception.Message, Status = exception.StatusCode });
        }
    }
}
