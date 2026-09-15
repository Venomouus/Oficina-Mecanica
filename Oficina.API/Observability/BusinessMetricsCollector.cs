using System.Data;
using Microsoft.EntityFrameworkCore;
using Oficina.Infrastructure.Persistence;

namespace Oficina.API.Observability;

public sealed class BusinessMetricsCollector(IServiceScopeFactory scopes, OficinaTelemetry telemetry,
    ILogger<BusinessMetricsCollector> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OficinaDbContext>();
                var today = DateTime.UtcNow.Date;
                var count = await db.OrdensServico.LongCountAsync(os => os.CriadaEm >= today && os.CriadaEm < today.AddDays(1), timeout.Token);
                var daily = await db.OrdensServico.Where(os => os.CriadaEm >= today.AddDays(-6) && os.CriadaEm < today.AddDays(1))
                    .GroupBy(os => os.CriadaEm.Date).Select(group => new { Day = group.Key, Count = group.LongCount() }).ToListAsync(timeout.Token);
                await db.Database.OpenConnectionAsync(timeout.Token);
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandTimeout = 5;
                command.CommandText = """
                    SELECT "Status", COUNT(*), AVG(EXTRACT(EPOCH FROM ("FinalizadaEm" - "IniciadaEm")))
                    FROM "HistoricoStatusOrdemServico"
                    WHERE "IniciadaEm" IS NOT NULL AND "FinalizadaEm" IS NOT NULL
                      AND "FinalizadaEm" >= CURRENT_TIMESTAMP - INTERVAL '24 hours'
                    GROUP BY "Status"
                    """;
                var periods = new Dictionary<string, PeriodMetric>();
                await using var reader = await command.ExecuteReaderAsync(timeout.Token);
                while (await reader.ReadAsync(timeout.Token))
                    periods[reader.GetString(0)] = new(reader.GetInt64(1), Convert.ToDouble(reader.GetValue(2)));
                telemetry.Update(new(count, true, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), periods)
                { Daily = daily.ToDictionary(row => row.Day.ToString("yyyy-MM-dd"), row => row.Count) });
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                telemetry.Update(telemetry.Snapshot with { Available = false });
                logger.LogWarning("Coleta de metricas de negocio falhou. error_type={ErrorType}", exception.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
