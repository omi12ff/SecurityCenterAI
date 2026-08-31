using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityCenterAI.Api.Contracts;
using SecurityCenterAI.Api.Security;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DashboardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var analyses = await dbContext.SecurityAnalyses
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.AnalyzedAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => new AnalysisSummaryResponse(
                item.Id,
                item.Score,
                item.AnalyzedAtUtc))
            .Take(5)
            .ToListAsync(cancellationToken);

        var totalAnalyses = await dbContext.SecurityAnalyses
            .AsNoTracking()
            .CountAsync(
                item => item.UserId == userId,
                cancellationToken);

        return Ok(new DashboardResponse(
            analyses.FirstOrDefault()?.Score,
            totalAnalyses,
            analyses));
    }
}
