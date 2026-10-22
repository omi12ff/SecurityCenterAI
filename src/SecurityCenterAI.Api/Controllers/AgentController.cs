using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Route("api/agent")]
public sealed class AgentController(DeviceService devices, AnalysisIngestionService analyses) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("pair")]
    [EnableRateLimiting("AgentPairing")]
    public async Task<IActionResult> Pair(PairDeviceRequest request, CancellationToken cancellationToken)
    {
        try { return StatusCode(StatusCodes.Status201Created, await devices.PairAsync(request.PairingCode, request.AgentVersion, cancellationToken)); }
        catch (DeviceOperationException exception) { return StatusCode(exception.StatusCode, new ProblemDetails { Title = exception.Message, Status = exception.StatusCode }); }
    }

    [Authorize(AuthenticationSchemes = DeviceAuthenticationHandler.SchemeName)]
    [HttpPost("heartbeat")]
    [EnableRateLimiting("AgentHeartbeat")]
    public async Task<IActionResult> Heartbeat(DeviceHeartbeatRequest request, CancellationToken cancellationToken)
    {
        var deviceId = Guid.Parse(User.FindFirstValue("device_id")!);
        var credentialId = Guid.Parse(User.FindFirstValue("credential_id")!);
        try { return await devices.HeartbeatAsync(deviceId, credentialId, request.AgentVersion, cancellationToken) ? NoContent() : Unauthorized(); }
        catch (DeviceOperationException exception) { return StatusCode(exception.StatusCode, new ProblemDetails { Title = exception.Message, Status = exception.StatusCode }); }
    }

    [Authorize(AuthenticationSchemes = DeviceAuthenticationHandler.SchemeName)]
    [HttpPost("analyses")]
    [EnableRateLimiting("AgentAnalysis")]
    [RequestSizeLimit(65_536)]
    public async Task<IActionResult> SubmitAnalysis(SubmitAnalysisRequest request, CancellationToken cancellationToken)
    {
        var deviceId = Guid.Parse(User.FindFirstValue("device_id")!);
        var credentialId = Guid.Parse(User.FindFirstValue("credential_id")!);
        try
        {
            var receipt = await analyses.SubmitAsync(deviceId, credentialId, request, cancellationToken);
            return StatusCode(receipt.AlreadyReceived ? StatusCodes.Status200OK : StatusCodes.Status201Created, receipt);
        }
        catch (DeviceOperationException exception)
        { return StatusCode(exception.StatusCode, new ProblemDetails { Title = exception.Message, Status = exception.StatusCode }); }
    }
}
