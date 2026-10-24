using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecurityCenterAI.Api.Services;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Route("api/account/export")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("AccountExport")]
public sealed class AccountExportController(AccountExportService exports) : ControllerBase
{
    private static readonly JsonSerializerOptions ExportJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue("sub")!);
            var export = await exports.ExportAsync(userId, cancellationToken);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(JsonSerializer.SerializeToUtf8Bytes(export, ExportJson), "application/json; charset=utf-8",
                $"securitycenterai-account-{userId:N}.json");
        }
        catch (DeviceOperationException exception)
        {
            return StatusCode(exception.StatusCode,
                new ProblemDetails { Title = exception.Message, Status = exception.StatusCode });
        }
    }
}
