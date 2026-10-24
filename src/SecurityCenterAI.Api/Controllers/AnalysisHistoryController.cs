using System.Security.Claims;
using System.Text.Json;
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
public sealed class AnalysisHistoryController(AnalysisHistoryService history) : ControllerBase
{
    private static readonly JsonSerializerOptions ExportJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    [HttpGet("export")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<IActionResult> Export(Guid deviceId, Guid analysisId, CancellationToken cancellationToken) =>
        Execute(async () =>
        {
            var report = await history.ExportAsync(UserId, deviceId, analysisId, cancellationToken);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(JsonSerializer.SerializeToUtf8Bytes(report, ExportJson), "application/json; charset=utf-8",
                $"securitycenterai-analysis-{analysisId:N}.json");
        });

    [HttpDelete]
    [RequestSizeLimit(1024)]
    public Task<IActionResult> Delete(Guid deviceId, Guid analysisId, DeleteAnalysisRequest request,
        CancellationToken cancellationToken) => Execute(async () =>
        {
            await history.DeleteAsync(UserId, deviceId, analysisId, request, cancellationToken);
            return NoContent();
        });

    private async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (DeviceOperationException exception)
        { return StatusCode(exception.StatusCode, new ProblemDetails { Title = exception.Message, Status = exception.StatusCode }); }
    }
}
