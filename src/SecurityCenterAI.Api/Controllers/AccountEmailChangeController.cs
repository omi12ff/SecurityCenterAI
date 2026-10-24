using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Route("api/account/email")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("DeviceAccount")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountEmailChangeController(AccountEmailChangeService emails) : ControllerBase
{
    private Guid OwnerId => Guid.Parse(User.FindFirstValue("sub")!);
    private Guid SessionId => Guid.Parse(User.FindFirstValue("sid")!);

    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Ok(await emails.StatusAsync(OwnerId, cancellationToken));
    }

    [HttpPost("request"), RequestSizeLimit(2048), EnableRateLimiting("AccountEmailChange")]
    public Task<IActionResult> Begin(RequestEmailChangeRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => emails.RequestAsync(OwnerId, SessionId, request, cancellationToken));

    [HttpPost("complete"), RequestSizeLimit(2048), EnableRateLimiting("AccountEmailChange")]
    public Task<IActionResult> Complete(CompleteEmailChangeRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => emails.CompleteAsync(OwnerId, SessionId, request, cancellationToken));

    [HttpPost("cancel"), RequestSizeLimit(1024)]
    public Task<IActionResult> Cancel(CancelEmailChangeRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => emails.CancelAsync(OwnerId, SessionId, request, cancellationToken));

    [AllowAnonymous, HttpPost("confirm-current"), RequestSizeLimit(1024), EnableRateLimiting("AuthPublic")]
    public Task<IActionResult> ConfirmCurrent(ConfirmEmailChangeRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => emails.ConfirmAsync(request.Token, true, request.Confirmed, cancellationToken));

    [AllowAnonymous, HttpPost("confirm-new"), RequestSizeLimit(1024), EnableRateLimiting("AuthPublic")]
    public Task<IActionResult> ConfirmNew(ConfirmEmailChangeRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => emails.ConfirmAsync(request.Token, false, request.Confirmed, cancellationToken));

    private async Task<IActionResult> ExecuteAsync(Func<Task> operation)
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        try { await operation(); return NoContent(); }
        catch (DeviceOperationException exception)
        { return StatusCode(exception.StatusCode, new ProblemDetails { Status = exception.StatusCode, Title = exception.Message }); }
    }
}
