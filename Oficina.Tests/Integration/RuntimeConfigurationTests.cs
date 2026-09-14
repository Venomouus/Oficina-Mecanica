using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Npgsql;
using Oficina.API.Hosting;
using Xunit;

namespace Oficina.Tests.Integration;

public class RuntimeConfigurationTests
{
    private static ConfigurationManager Config() => new()
    {
        ["AwsRuntime:Database"] = "oficina_staging",
        ["AwsRuntime:DatabaseHost"] = "database.test.rds.amazonaws.com",
        ["AwsRuntime:DatabaseSecretArn"] = "database-secret",
        ["AwsRuntime:ApiSecretArn"] = "api-secret"
    };

    [Fact]
    public async Task Migration_reads_only_its_credential_and_preserves_password_characters()
    {
        var config = Config();
        const string password = "semicolon;quote'\"equals=backslash\\password";
        var reads = new List<string>();
        await AwsRuntimeConfiguration.LoadAsync(config, true, (arn, _) =>
        {
            reads.Add(arn);
            return Task.FromResult(JsonSerializer.Serialize(new { username = "oficina_staging_migrations", password }));
        });
        Assert.Equal(new[] { "database-secret" }, reads);
        var connection = new NpgsqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection"));
        Assert.Equal(password, connection.Password);
        Assert.Equal(SslMode.VerifyFull, connection.SslMode);
        Assert.Equal("oficina_staging_migrations", connection.Username);
        Assert.Null(config["Jwt:Key"]);
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("oficina_producao_app")]
    [InlineData("oficina_staging_migrations")]
    public async Task Runtime_rejects_master_other_environment_and_migration_credentials(string username)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => AwsRuntimeConfiguration.LoadAsync(Config(), false,
            (_, _) => Task.FromResult(JsonSerializer.Serialize(new { username, password = "not-logged" }))));
    }

    [Fact]
    public async Task Runtime_ignores_extra_secret_fields_and_does_not_fallback_on_failure()
    {
        var config = Config();
        await AwsRuntimeConfiguration.LoadAsync(config, false, (arn, _) => Task.FromResult(arn == "database-secret"
            ? """{"username":"oficina_staging_app","password":"example-long-password","host":"attacker.invalid"}"""
            : """{"jwtKey":"example-long-key","adminUser":"operator","adminPassword":"example-password","budgetToken":"example-token","ClienteJwt:Enabled":"false"}"""));
        Assert.Equal("database.test.rds.amazonaws.com", new NpgsqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection")).Host);
        Assert.Null(config["ClienteJwt:Enabled"]);
        await Assert.ThrowsAsync<IOException>(() => AwsRuntimeConfiguration.LoadAsync(Config(), false,
            (_, _) => throw new IOException("unavailable")));
    }

    [Fact]
    public async Task Production_accepts_separate_runtime_and_job_configuration_but_rejects_tls_downgrade()
    {
        var config = Config();
        config["Jwt:Issuer"] = "oficina-staging";
        config["Jwt:Audience"] = "oficina-staging-admin";
        config["ClienteJwt:Enabled"] = "true";
        config["ClienteJwt:Issuer"] = "https://example.execute-api.us-east-1.amazonaws.com";
        config["ClienteJwt:Audience"] = "oficina-api";
        await AwsRuntimeConfiguration.LoadAsync(config, false, (arn, _) => Task.FromResult(arn == "database-secret"
            ? """{"username":"oficina_staging_app","password":"example-long-password"}"""
            : JsonSerializer.Serialize(new { jwtKey = new string('k', 40), adminUser = "operator", adminPassword = new string('p', 24), budgetToken = new string('b', 40) })));
        RuntimeConfiguration.Validate(config, new ProductionEnvironment(), false);
        Assert.Throws<InvalidOperationException>(() => RuntimeConfiguration.Validate(config, new ProductionEnvironment(), true));
        var connection = new NpgsqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection")) { SslMode = SslMode.Require };
        config["ConnectionStrings:DefaultConnection"] = connection.ConnectionString;
        Assert.Throws<InvalidOperationException>(() => RuntimeConfiguration.Validate(config, new ProductionEnvironment(), false));
        var migration = Config();
        await AwsRuntimeConfiguration.LoadAsync(migration, true, (_, _) => Task.FromResult(
            """{"username":"oficina_staging_migrations","password":"example-long-password"}"""));
        RuntimeConfiguration.Validate(migration, new ProductionEnvironment(), true);
    }

    [Fact]
    public void Production_rejects_automatic_migrations_and_development_defaults()
    {
        var config = Config();
        config["Database:MigrateOnStartup"] = "true";
        Assert.Throws<InvalidOperationException>(() => RuntimeConfiguration.Validate(config, new ProductionEnvironment(), false));
        config["Database:MigrateOnStartup"] = "false";
        config["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=oficina;Username=postgres;Password=postgres";
        Assert.Throws<InvalidOperationException>(() => RuntimeConfiguration.Validate(config, new ProductionEnvironment(), false));
    }

    [Fact]
    public async Task Live_and_ready_endpoints_are_anonymous_and_do_not_require_migration_history()
    {
        await using var factory = new OficinaApiFactory();
        var client = factory.CreateClient();
        Assert.True((await client.GetAsync("/health/live")).IsSuccessStatusCode);
        Assert.True((await client.GetAsync("/health")).IsSuccessStatusCode);
    }

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Oficina.API";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
