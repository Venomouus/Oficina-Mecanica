using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Oficina.API.Observability;

public sealed class OficinaTelemetry : IDisposable
{
    public const string Name = "Oficina.API";
    public static readonly ActivitySource Activities = new(Name);
    private readonly Meter meter = new(Name);
    public Counter<long> Requests { get; }
    public Counter<long> Failures { get; }
    public Histogram<double> Duration { get; }
    private BusinessSnapshot snapshot = BusinessSnapshot.Empty;
    public BusinessSnapshot Snapshot => Volatile.Read(ref snapshot);
    public void Update(BusinessSnapshot value) => Volatile.Write(ref snapshot, value);

    public OficinaTelemetry()
    {
        Requests = meter.CreateCounter<long>("oficina_http_requests", description: "HTTP requests by route template, never customer IDs");
        Failures = meter.CreateCounter<long>("oficina_os_failures", description: "5xx on order service routes");
        Duration = meter.CreateHistogram<double>("oficina_http_duration", "s");
        meter.CreateObservableGauge("oficina_os_today", () => Snapshot.Today, description: "Orders created today, UTC calendar day");
        meter.CreateObservableGauge("oficina_os_daily", () => Snapshot.Daily.Select(p =>
            new Measurement<long>(p.Value, new KeyValuePair<string, object?>("day", p.Key))), description: "Orders per UTC calendar day, last seven days");
        meter.CreateObservableGauge("oficina_business_collection_ok", () => Snapshot.Available ? 1 : 0);
        meter.CreateObservableGauge("oficina_business_last_success_unixtime", () => Snapshot.LastSuccessUnix);
        meter.CreateObservableGauge("oficina_os_status_duration_seconds", () => Snapshot.Periods.Select(p =>
            new Measurement<double>(p.Value.MeanSeconds, new KeyValuePair<string, object?>("status", p.Key))));
        meter.CreateObservableGauge("oficina_os_status_samples", () => Snapshot.Periods.Select(p =>
            new Measurement<long>(p.Value.Count, new KeyValuePair<string, object?>("status", p.Key))));
        meter.CreateObservableGauge("oficina_process_memory_bytes", () => Environment.WorkingSet);
        var process = Process.GetCurrentProcess();
        meter.CreateObservableCounter("oficina_process_cpu_seconds", () => process.TotalProcessorTime.TotalSeconds);
    }
    public void Dispose() => meter.Dispose();
}

public sealed record PeriodMetric(long Count, double MeanSeconds);
public sealed record BusinessSnapshot(long Today, bool Available, long LastSuccessUnix, IReadOnlyDictionary<string, PeriodMetric> Periods)
{
    public IReadOnlyDictionary<string, long> Daily { get; init; } = new Dictionary<string, long>();
    public static BusinessSnapshot Empty => new(0, false, 0, new Dictionary<string, PeriodMetric>());
}

public static class ObservabilityRegistration
{
    public static IServiceCollection AddOficinaObservability(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<OficinaTelemetry>();
        if (!config.GetValue<bool>("Observability:Enabled")) return services;
        services.AddHostedService<BusinessMetricsCollector>();
        services.AddOpenTelemetry().ConfigureResource(resource => resource.AddService(OficinaTelemetry.Name))
            .WithTracing(tracing => tracing.AddSource(OficinaTelemetry.Name, "Npgsql")
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation().AddOtlpExporter())
            .WithMetrics(metrics => metrics.AddMeter(OficinaTelemetry.Name)
                .AddAspNetCoreInstrumentation().AddOtlpExporter());
        services.AddLogging(logging => logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(OficinaTelemetry.Name));
            options.AddOtlpExporter();
        }));
        return services;
    }
}
