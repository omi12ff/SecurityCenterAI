using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Route("api/account")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("AccountDeletion")]
public sealed class AccountDeletionController(AccountDeletionService accounts) : ControllerBase
{
    [HttpDelete]
    [RequestSizeLimit(2048)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Delete(DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await accounts.DeleteAsync(Guid.Parse(User.FindFirstValue("sub")!), Guid.Parse(User.FindFirstValue("sid")!),
                request, cancellationToken);
            return NoContent();
        }
        catch (DeviceOperationException exception)
        { return StatusCode(exception.StatusCode, new ProblemDetails { Status = exception.StatusCode, Title = exception.Message }); }
    }
}
