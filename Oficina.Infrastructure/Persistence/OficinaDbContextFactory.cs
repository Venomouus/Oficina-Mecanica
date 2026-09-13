using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Oficina.Infrastructure.Persistence;

// Permite gerar/revisar migrations sem iniciar a API e sem executar Database.Migrate.
public sealed class OficinaDbContextFactory : IDesignTimeDbContextFactory<OficinaDbContext>
{
    public OficinaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=oficina;Username=postgres";
        return new OficinaDbContext(new DbContextOptionsBuilder<OficinaDbContext>()
            .UseNpgsql(connectionString).Options);
    }
}
