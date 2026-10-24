using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Route("api/account/sessions")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("DeviceAccount")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountSessionsController(AccountSessionService sessions, AccountService accounts) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);
    private Guid SessionId => Guid.Parse(User.FindFirstValue("sid")!);

    [HttpGet]
    public async Task<AccountSessionListResponse> Get([FromQuery, Range(1, AccountSessionService.MaxPage)] int page = 1,
        CancellationToken cancellationToken = default)
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return await sessions.ListAsync(UserId, SessionId, page, cancellationToken);
    }

    [HttpPost("{id:guid}/revoke")]
    [RequestSizeLimit(1024)]
    public async Task<IActionResult> Revoke(Guid id, RevokeAccountSessionRequest request, CancellationToken cancellationToken)
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (!request.Confirmed) return BadRequest(new ProblemDetails { Status = 400, Title = "Confirmá el cierre de la sesión." });
        if (!await accounts.RevokeOwnedSessionAsync(UserId, id, cancellationToken))
            return NotFound(new ProblemDetails { Status = 404, Title = "No encontramos esa sesión." });
        return Ok(new RevokeAccountSessionResponse(id == SessionId));
    }
}
