using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Oficina.Infrastructure.Persistence;

namespace Oficina.API.Hosting;

public sealed class DatabaseReadinessCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OficinaDbContext>();
            // Runtime cannot read __EFMigrationsHistory. Exercise a business query including Ativo.
            await db.Clientes.AsNoTracking().Select(cliente => new { cliente.Id, cliente.Ativo }).Take(1)
                .ToListAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch { return HealthCheckResult.Unhealthy("Banco indisponivel ou schema ainda nao preparado."); }
    }
}
