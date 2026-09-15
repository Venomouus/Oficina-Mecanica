using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Oficina.Application.Interfaces;
using Oficina.Application.Services;
using Oficina.Infrastructure.Persistence;
using Oficina.Infrastructure.Repositories;
using Oficina.API.Security;
using Oficina.API.Hosting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Oficina.API.Observability;

var migrate = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--migrate").ToArray());
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
try
{
    if (builder.Configuration.GetValue<bool>("AwsRuntime:Enabled"))
        await AwsRuntimeConfiguration.LoadAsync(builder.Configuration, migrate);
    RuntimeConfiguration.Validate(builder.Configuration, builder.Environment, migrate);
}
catch
{
    Console.Error.WriteLine("Configuracao de inicializacao invalida ou segredo indisponivel. Verifique os contratos, IRSA e Secrets Manager.");
    Environment.ExitCode = 1;
    return;
}

if (migrate)
{
    Environment.ExitCode = await MigrationRunner.RunAsync(builder.Configuration.GetConnectionString("DefaultConnection")!);
    return;
}

builder.Services.AddControllers();
builder.Services.AddOficinaObservability(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("database", tags: new[] { "ready" }, timeout: TimeSpan.FromSeconds(3));
builder.Services.AddScoped<ClienteService>();
builder.Services.AddScoped<VeiculoService>();
builder.Services.AddScoped<ServicoService>();
builder.Services.AddScoped<PecaInsumoService>();
builder.Services.AddScoped<OrdemServicoService>();
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IVeiculoRepository, VeiculoRepository>();
builder.Services.AddScoped<IServicoRepository, ServicoRepository>();
builder.Services.AddScoped<IPecaInsumoRepository, PecaInsumoRepository>();
builder.Services.AddScoped<IOrdemServicoRepository, OrdemServicoRepository>();


builder.Services.AddAutenticacaoOficina(builder.Configuration, builder.Environment);

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Oficina Mecanica API",
        Version = "v1",
        Description = "MVP para atendimento, ordens de servico, clientes, veiculos, pecas e servicos."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT administrativo de /api/auth/login ou JWT de cliente de POST /auth/cpf no serverless."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<OficinaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();
app.UseRouting();
app.UseMiddleware<RequestTelemetryMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OficinaDbContext>();
    db.Database.Migrate();
}


app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program
{
}


