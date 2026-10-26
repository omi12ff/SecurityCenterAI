using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;
using SecurityCenterAI.Api.Services.Ai;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Route("api/devices/{deviceId:guid}/analyses/{analysisId:guid}/ai-explanation")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("DeviceAccount")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AnalysisAiExplanationController(AnalysisAiExplanationService explanations) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    [HttpGet]
    public Task<IActionResult> Availability(Guid deviceId, Guid analysisId, CancellationToken cancellationToken) =>
        Execute(async () => Ok(await explanations.AvailabilityAsync(UserId, deviceId, analysisId, cancellationToken)));

    [HttpPost]
    [RequestSizeLimit(1024)]
    public Task<IActionResult> Explain(Guid deviceId, Guid analysisId, AiExplanationRequest request,
        CancellationToken cancellationToken) =>
        Execute(async () => Ok(await explanations.ExplainAsync(UserId, deviceId, analysisId, request, cancellationToken)));

    private async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (DeviceOperationException exception)
        { return StatusCode(exception.StatusCode, new ProblemDetails { Title = exception.Message, Status = exception.StatusCode }); }
    }
}
