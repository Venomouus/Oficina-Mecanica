using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Oficina.Infrastructure.Persistence;

namespace Oficina.Tests.Integration;

public class OficinaApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"oficina-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<OficinaDbContext>>();

            services.AddDbContext<OficinaDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
