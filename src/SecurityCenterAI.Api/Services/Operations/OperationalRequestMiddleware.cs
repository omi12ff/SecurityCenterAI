using System.Data.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SecurityCenterAI.Api.Services.Operations;

public sealed class OperationalRequestMiddleware(RequestDelegate next, ILogger<OperationalRequestMiddleware> logger)
{
    public const string RequestIdHeader = "X-Request-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        // Never trust incoming request/trace identifiers for the diagnostic record.
        var requestId = Guid.NewGuid().ToString("N");
        context.TraceIdentifier = requestId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[RequestIdHeader] = requestId;
            return Task.CompletedTask;
        });
        var started = Stopwatch.GetTimestamp();
        var failure = "none";
        var status = 500;
        try
        {
            await next(context);
            status = context.Response.StatusCode;
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            status = 499;
            failure = "request_cancelled";
            context.Abort();
        }
        catch (Exception exception)
        {
            status = exception is BadHttpRequestException bad && bad.StatusCode is 400 or 413 or 414 or 431
                ? bad.StatusCode : 500;
            failure = exception switch
            {
                DbException or DbUpdateException => "database_failure",
                TimeoutException => "dependency_timeout",
                BadHttpRequestException => "invalid_request",
                _ => "unhandled_failure"
            };
            if (context.Response.HasStarted)
            {
                status = context.Response.StatusCode;
                context.Abort();
            }
            else
            {
                context.Response.Clear();
                context.Response.StatusCode = status;
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                try
                {
                    await context.Response.WriteAsJsonAsync(new ProblemDetails
                    {
                        Status = status,
                        Title = status == 500 ? "No se pudo completar la solicitud. Volvé a intentar más tarde."
                            : "No se pudo procesar el contenido de la solicitud.",
                        Extensions = { ["requestId"] = requestId }
                    }, options: (System.Text.Json.JsonSerializerOptions?)null,
                        contentType: "application/problem+json", cancellationToken: context.RequestAborted);
                }
                catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { context.Abort(); }
            }
        }
        finally
        {
            // Route templates are application code. Raw paths, query strings,
            // headers, remote addresses, bodies, scopes and exceptions never enter logs.
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            var method = context.Request.Method switch
            {
                "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS" => context.Request.Method,
                _ => "OTHER"
            };
            var level = status >= 500 || failure is "database_failure" or "dependency_timeout" or "unhandled_failure" ? LogLevel.Error
                : status >= 400 ? LogLevel.Warning : LogLevel.Information;
            logger.Log(level, failure == "none" ? SafeOperationalLogging.RequestCompleted : SafeOperationalLogging.RequestFailed,
                "HTTP {Method} {Route}; status {StatusCode}; duration {ElapsedMs} ms; request {RequestId}; failure {FailureCode}.",
                method, route, status, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, requestId, failure);
        }
    }
}
