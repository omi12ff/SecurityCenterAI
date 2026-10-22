using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
        {
            return Unauthorized();
        }

        var analyses = await dbContext.SecurityAnalyses
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.AnalyzedAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => new { item.Id, item.Score, item.AnalyzedAtUtc, item.DeviceId, item.Source,
                item.ScoringVersion, item.CoveragePercent, item.PassedCount, item.AttentionCount, item.UnknownCount,
                item.NotApplicableCount, item.AgentVersion, item.StartedAtUtc, item.CompletedAtUtc, item.ReportId })
            .Take(5)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            SecurityScore = analyses.FirstOrDefault()?.Score,
            LastAnalysis = analyses.FirstOrDefault(),
            TotalAnalyses = await dbContext.SecurityAnalyses.CountAsync(
                item => item.UserId == userId,
                cancellationToken),
            RecentAnalyses = analyses
        });
    }
}
