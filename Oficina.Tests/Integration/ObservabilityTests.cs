using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Oficina.API.Observability;
using Xunit;

namespace Oficina.Tests.Integration;

public class ObservabilityTests
{
    [Fact]
    public async Task Falha_Retorna500SemVazarMensagemEUsaTraceId()
    {
        using var activity = new Activity("teste").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var telemetry = new OficinaTelemetry();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["X-Correlation-ID"] = "senha-enviada-pelo-cliente";
        var middleware = new RequestTelemetryMiddleware(_ => throw new InvalidOperationException("Password=segredo"),
            NullLogger<RequestTelemetryMiddleware>.Instance);

        await middleware.InvokeAsync(context, telemetry);

        Assert.Equal(500, context.Response.StatusCode);
        Assert.Equal(activity.TraceId.ToString(), context.Response.Headers["X-Correlation-ID"].ToString());
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("segredo", body);
        Assert.DoesNotContain("senha-enviada", body);
        Assert.Contains(activity.TraceId.ToString(), body);
    }
}
