using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Route("api/devices/{deviceId:guid}/analyses/{analysisId:guid}")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("DeviceAccount")]
public sealed class AnalysisGuidanceController(AnalysisGuidanceService guidance) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    [HttpGet("explanation")]
    public Task<IActionResult> Explain(Guid deviceId, Guid analysisId, CancellationToken cancellationToken) =>
        Execute(async () => Ok(await guidance.ExplainAsync(UserId, deviceId, analysisId, cancellationToken)));

    [HttpPost("feedback")]
    [RequestSizeLimit(8192)]
    public Task<IActionResult> Feedback(Guid deviceId, Guid analysisId, AnalysisFeedbackRequest request, CancellationToken cancellationToken) =>
        Execute(async () => Ok(await guidance.SaveFeedbackAsync(UserId, deviceId, analysisId, request, cancellationToken)));

    private async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (DeviceOperationException exception)
        { return StatusCode(exception.StatusCode, new ProblemDetails { Title = exception.Message, Status = exception.StatusCode }); }
    }
}
