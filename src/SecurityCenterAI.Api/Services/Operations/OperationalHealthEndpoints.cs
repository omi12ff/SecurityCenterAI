using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SecurityCenterAI.Api.Services.Operations;

public static class OperationalHealthEndpoints
{
    public static void MapOperationalHealth(this WebApplication app)
    {
        app.MapGet("/health", (HttpContext context) =>
        {
            PrivateResponse(context);
            return Results.Ok(new { status = "healthy" });
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResponseWriter = async (context, report) =>
            {
                PrivateResponse(context);
                await context.Response.WriteAsJsonAsync(new
                {
                    status = report.Status == HealthStatus.Healthy ? "ready" : "not_ready",
                    dependencies = new
                    {
                        postgresql = report.Entries.TryGetValue("postgresql", out var database) && database.Status == HealthStatus.Healthy
                            ? "available" : "unavailable"
                    }
                }, context.RequestAborted);
            }
        }).AllowAnonymous().RequireRateLimiting("Readiness");
    }

    private static void PrivateResponse(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }
}
