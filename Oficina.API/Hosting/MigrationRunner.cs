using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Oficina.Infrastructure.Persistence;

namespace Oficina.API.Hosting;

public static class MigrationRunner
{
    public static async Task<int> RunAsync(string connectionString)
    {
        try
        {
            var options = new DbContextOptionsBuilder<OficinaDbContext>().UseNpgsql(connectionString)
                .UseLoggerFactory(NullLoggerFactory.Instance).Options;
            await using var db = new OficinaDbContext(options);
            await db.Database.OpenConnectionAsync();
            // Session lock on the same physical connection, also covering retries/concurrent Jobs.
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(714009821)";
            if (!Equals(await command.ExecuteScalarAsync(), true))
                throw new InvalidOperationException("Outra migracao esta em andamento.");
            try { await db.Database.MigrateAsync(); }
            finally
            {
                command.CommandText = "SELECT pg_advisory_unlock(714009821)";
                await command.ExecuteScalarAsync();
            }
            Console.WriteLine("Migrations concluidas.");
            return 0;
        }
        catch (Exception exception)
        {
            // Never log SQL, connection strings or nested exceptions containing credentials/data.
            var sqlState = (exception as PostgresException)?.SqlState;
            Console.Error.WriteLine($"Migrations falharam. SQLSTATE: {sqlState ?? "indisponivel"}. Verifique conectividade, usuario e schema.");
            return 1;
        }
    }
}
