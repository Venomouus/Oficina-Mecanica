using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Npgsql;

namespace Oficina.API.Hosting;

public static class AwsRuntimeConfiguration
{
    public static async Task LoadAsync(ConfigurationManager config, bool migrate)
    {
        // Academy cannot create the IRSA roles used in a regular AWS account.
        // The deployment identity copies only the required secret into a mounted
        // Kubernetes Secret. Pods do not receive Academy credentials or node IAM access.
        var secretDirectory = config["AwsRuntime:SecretDirectory"];
        if (!string.IsNullOrWhiteSpace(secretDirectory))
        {
            await LoadAsync(config, migrate, (arn, token) => File.ReadAllTextAsync(
                Path.Combine(secretDirectory, arn == Required(config, "AwsRuntime:DatabaseSecretArn")
                    ? "database.json" : "api.json"), token));
            return;
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var client = new AmazonSecretsManagerClient(new AmazonSecretsManagerConfig { MaxErrorRetry = 1 });
        await LoadAsync(config, migrate, async (arn, token) =>
        {
            var response = await client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = arn }, token);
            return response.SecretString ?? throw new InvalidOperationException("Segredo JSON obrigatorio.");
        }, timeout.Token);
    }

    // Only selected secret fields can override configuration; no arbitrary JSON configuration provider.
    public static async Task LoadAsync(ConfigurationManager config, bool migrate,
        Func<string, CancellationToken, Task<string>> readSecret, CancellationToken cancellationToken = default)
    {
        var database = Required(config, "AwsRuntime:Database");
        if (database is not ("oficina_staging" or "oficina_producao"))
            throw new InvalidOperationException("Banco de ambiente invalido.");
        var expectedUser = $"{database}_{(migrate ? "migrations" : "app")}";
        using var secret = JsonDocument.Parse(await readSecret(Required(config, "AwsRuntime:DatabaseSecretArn"), cancellationToken));
        var username = secret.RootElement.GetProperty("username").GetString();
        var password = secret.RootElement.GetProperty("password").GetString();
        if (username != expectedUser || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Credencial nao corresponde a finalidade do processo.");
        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = Required(config, "AwsRuntime:DatabaseHost"), Port = 5432, Database = database,
            Username = username, Password = password, SslMode = SslMode.VerifyFull,
            RootCertificate = Path.Combine(AppContext.BaseDirectory, "certificates", "rds-global-bundle.pem"),
            Timeout = 5, CommandTimeout = migrate ? 120 : 15, MaxPoolSize = migrate ? 1 : 10,
            IncludeErrorDetail = false, ApplicationName = migrate ? "oficina-migrations" : "oficina-api"
        };
        var values = new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connection.ConnectionString };
        if (!migrate)
        {
            using var api = JsonDocument.Parse(await readSecret(Required(config, "AwsRuntime:ApiSecretArn"), cancellationToken));
            foreach (var (field, key) in new[] { ("jwtKey", "Jwt:Key"), ("adminUser", "Jwt:AdminUser"),
                         ("adminPassword", "Jwt:AdminPassword"), ("budgetToken", "ExternalIntegrations:BudgetToken") })
                values[key] = api.RootElement.GetProperty(field).GetString();
        }
        config.AddInMemoryCollection(values);
    }

    private static string Required(IConfiguration config, string key) =>
        !string.IsNullOrWhiteSpace(config[key]) ? config[key]! : throw new InvalidOperationException($"Campo obrigatorio: {key}");
}
