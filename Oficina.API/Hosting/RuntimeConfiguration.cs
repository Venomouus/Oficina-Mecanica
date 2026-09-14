using System.Text;
using Npgsql;
using Oficina.API.Security;

namespace Oficina.API.Hosting;

public static class RuntimeConfiguration
{
    public static void Validate(IConfiguration config, IHostEnvironment environment, bool migrate)
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Testing")) return;
        if (config.GetValue<bool>("Database:MigrateOnStartup"))
            throw new InvalidOperationException("Migrations automaticas somente em Development.");

        var connection = new NpgsqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection"));
        var purpose = migrate ? "migrations" : "app";
        if (string.IsNullOrWhiteSpace(connection.Host) || string.IsNullOrWhiteSpace(connection.Password)
            || connection.Database is not ("oficina_staging" or "oficina_producao")
            || connection.Username != $"{connection.Database}_{purpose}"
            || connection.SslMode != SslMode.VerifyFull || !File.Exists(connection.RootCertificate))
            throw new InvalidOperationException("Banco, usuario ou TLS invalidos para o ambiente.");
        if (migrate) return;

        var key = config["Jwt:Key"] ?? "";
        if (Encoding.UTF8.GetByteCount(key) < 32 || key.Contains("trocar-esta-chave", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(config["Jwt:Issuer"]) || string.IsNullOrWhiteSpace(config["Jwt:Audience"])
            || string.IsNullOrWhiteSpace(config["Jwt:AdminUser"])
            || (config["Jwt:AdminPassword"] ?? "").Length < 16 || config["Jwt:AdminPassword"] == "Admin@123"
            || (config["ExternalIntegrations:BudgetToken"] ?? "").Length < 32)
            throw new InvalidOperationException("Credenciais administrativas ausentes ou inseguras.");
        var client = config.GetSection("ClienteJwt").Get<ClienteJwtSettings>() ?? new();
        if (!client.Enabled || !client.Valido(environment))
            throw new InvalidOperationException("Emissor de clientes HTTPS obrigatorio.");
    }
}
