using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace Oficina.API.Observability;

public sealed class RequestTelemetryMiddleware(RequestDelegate next, ILogger<RequestTelemetryMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, OficinaTelemetry telemetry)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        // Do not log arbitrary correlation headers, URLs, bodies, JWTs or personal data.
        context.Response.Headers["X-Correlation-ID"] = traceId;
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["trace_id"] = traceId, ["route"] = route });
        var timer = Stopwatch.StartNew();
        try { await next(context); }
        catch (Exception exception) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            // Keep the exception type, not the message/stack which may contain secrets or SQL data.
            logger.LogError("Falha no processamento. error_type={ErrorType}", exception.GetType().Name);
            context.Response.Clear();
            context.Response.StatusCode = 500;
            context.Response.Headers["X-Correlation-ID"] = traceId;
            await context.Response.WriteAsJsonAsync(new { message = "Falha interna no processamento.", correlationId = traceId });
        }
        finally
        {
            var status = context.Response.StatusCode;
            var tags = new TagList { { "route", route }, { "method", context.Request.Method }, { "status_code", status } };
            telemetry.Requests.Add(1, tags);
            telemetry.Duration.Record(timer.Elapsed.TotalSeconds, tags);
            if (route.Contains("ordens-servico", StringComparison.OrdinalIgnoreCase))
                telemetry.Failures.Add(status >= 500 ? 1 : 0, new TagList { { "route", route } });
            logger.LogInformation("HTTP {Method} {Route} status={StatusCode} duration_ms={DurationMs} trace_id={TraceId}",
                context.Request.Method, route, status, timer.Elapsed.TotalMilliseconds, traceId);
        }
    }
}
