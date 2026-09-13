using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Oficina.Application.Interfaces;
using Oficina.Application.Services;
using Oficina.Infrastructure.Persistence;
using Oficina.Infrastructure.Repositories;
using Oficina.API.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OficinaDbContext>();
    db.Database.Migrate();
}


app.MapHealthChecks("/health");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program
{
}


