using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Services;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("DeviceAccount")]
public sealed class DevicesController(DeviceService devices) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await devices.ListAsync(UserId, cancellationToken));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken) => Execute(async () =>
        Ok(await devices.DetailAsync(UserId, id, cancellationToken)));

    [HttpPost]
    public Task<IActionResult> Create(CreateDeviceRequest request, CancellationToken cancellationToken) => Execute(async () =>
    {
        var response = await devices.CreateAsync(UserId, request.Name, cancellationToken);
        return CreatedAtAction(nameof(Detail), new { id = response.Device.Id }, response);
    });

    [HttpPost("{id:guid}/pairing-code")]
    public Task<IActionResult> PairingCode(Guid id, CancellationToken cancellationToken) => Execute(async () =>
        Ok(await devices.ReissueAsync(UserId, id, cancellationToken)));

    [HttpPost("{id:guid}/revoke")]
    public Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken) => Execute(async () =>
    { await devices.RevokeAsync(UserId, id, cancellationToken); return NoContent(); });

    private async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (DeviceOperationException exception) { return StatusCode(exception.StatusCode, new ProblemDetails { Title = exception.Message, Status = exception.StatusCode }); }
    }
}
