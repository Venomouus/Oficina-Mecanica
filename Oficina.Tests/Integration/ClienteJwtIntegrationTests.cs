using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Oficina.API.Security;
using Oficina.Application.Interfaces;
using Oficina.Domain.Entities;
using Oficina.Domain.Enums;
using Oficina.Infrastructure.Persistence;
using Oficina.Infrastructure.Repositories;
using Xunit;

namespace Oficina.Tests.Integration;

public sealed class ClienteJwtIntegrationTests
{
    [Fact]
    public async Task Cliente_ConsultaSomenteSuasOrdens_EmTodasAsRotas()
    {
        using var factory = new ClienteJwtFactory();
        using var client = factory.CreateClient();
        var dados = await factory.SemearAsync();
        Autenticar(client, factory.Token(dados.Cliente.Id));

        foreach (var rota in new[] { "/api/ordens-servico/{0}", "/api/ordens-servico/{0}/status",
                     "/api/ordens-servico/consulta/{0}", "/api/minhas-ordens-servico/{0}" })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(string.Format(rota, dados.Ordem.Id))).StatusCode);
            // O CPF do outro cliente nao substitui a identidade do JWT.
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(
                string.Format(rota, dados.OutraOrdem.Id) + "?cpfCnpj=12345678909")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(string.Format(rota, Guid.NewGuid()))).StatusCode);
        }
        var lista = await client.GetFromJsonAsync<JsonElement>("/api/minhas-ordens-servico");
        Assert.Single(lista.EnumerateArray());
        Assert.Equal(dados.Ordem.Id, lista[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Cliente_CriaEAprovaSuaOrdem_SemEscolherOutroCliente()
    {
        using var factory = new ClienteJwtFactory();
        using var client = factory.CreateClient();
        var dados = await factory.SemearAsync();
        Autenticar(client, factory.Token(dados.Cliente.Id));
        var pedido = new
        {
            veiculo = new { placa = "JWT1234", marca = "Fiat", modelo = "Uno", ano = 2020 },
            servicosIds = new[] { dados.Servico.Id }, pecas = Array.Empty<object>(), observacoes = "Teste JWT"
        };
        var criado = await client.PostAsJsonAsync("/api/minhas-ordens-servico", pedido);
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        var body = await criado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Aguardando Aprovacao", body.GetProperty("status").GetString());
        var id = body.GetProperty("id").GetGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OficinaDbContext>();
            Assert.Equal(dados.Cliente.Id, (await db.OrdensServico.FindAsync(id))!.ClienteId);
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(criado.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/minhas-ordens-servico/{id}/aprovar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/ordens-servico/{id}/aprovar", null)).StatusCode);

        var adulterado = JsonSerializer.SerializeToNode(pedido)!;
        adulterado["clienteId"] = dados.OutroCliente.Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/minhas-ordens-servico", adulterado)).StatusCode);
        adulterado.AsObject().Remove("clienteId");
        adulterado["veiculo"]!["placa"] = "OUT1234";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/minhas-ordens-servico", adulterado)).StatusCode);

        foreach (var prefixo in new[] { "/api/minhas-ordens-servico", "/api/ordens-servico" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(
                $"{prefixo}/{dados.OutraOrdem.Id}/aprovar?cpfCnpj=12345678909", null)).StatusCode);
        using var finalScope = factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<OficinaDbContext>();
        Assert.Equal(StatusOrdemServico.AguardandoAprovacao, (await finalDb.OrdensServico.FindAsync(dados.OutraOrdem.Id))!.Status);
        Assert.Equal(3, await finalDb.OrdensServico.CountAsync());
    }

    [Theory]
    [InlineData("GET", "/api/clientes")]
    [InlineData("GET", "/api/veiculos")]
    [InlineData("GET", "/api/servicos")]
    [InlineData("GET", "/api/pecas-insumos")]
    [InlineData("GET", "/api/ordens-servico")]
    [InlineData("GET", "/api/ordens-servico/metricas/tempo-medio")]
    [InlineData("GET", "/api/ordens-servico/{0}/historico")]
    [InlineData("POST", "/api/ordens-servico")]
    [InlineData("PATCH", "/api/ordens-servico/{0}/status")]
    [InlineData("POST", "/api/clientes")]
    public async Task Cliente_NaoAcessaOperacoesAdministrativas(string metodo, string rota)
    {
        using var factory = new ClienteJwtFactory();
        using var client = factory.CreateClient();
        var dados = await factory.SemearAsync();
        Autenticar(client, factory.Token(dados.Cliente.Id));
        using var request = new HttpRequestMessage(new HttpMethod(metodo), string.Format(rota, dados.Ordem.Id));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
    }

    [Theory]
    [InlineData("ausente")]
    [InlineData("malformado")]
    [InlineData("expirado")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("assinatura")]
    [InlineData("role")]
    [InlineData("scope")]
    [InlineData("sub")]
    [InlineData("tipo")]
    [InlineData("algoritmo")]
    public async Task TokenInvalido_OuCpfSemToken_NaoAutoriza(string defeito)
    {
        using var factory = new ClienteJwtFactory();
        using var client = factory.CreateClient();
        var dados = await factory.SemearAsync();
        if (defeito != "ausente") Autenticar(client, factory.Token(dados.Cliente.Id, defeito));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(
            $"/api/ordens-servico/{dados.Ordem.Id}/status?cpfCnpj=52998224725")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(
            $"/api/ordens-servico/{dados.Ordem.Id}/aprovar?cpfCnpj=52998224725", null)).StatusCode);
    }

    [Fact]
    public async Task Desativacao_InvalidaAcessoComTokenJaEmitido_EClienteInexistenteEhNegado()
    {
        using var factory = new ClienteJwtFactory();
        using var client = factory.CreateClient();
        var dados = await factory.SemearAsync();
        Autenticar(client, factory.Token(dados.Cliente.Id));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/minhas-ordens-servico")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OficinaDbContext>();
            (await db.Clientes.FindAsync(dados.Cliente.Id))!.AlterarStatus(false);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/minhas-ordens-servico")).StatusCode);
        Autenticar(client, factory.Token(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/minhas-ordens-servico")).StatusCode);
    }

    [Fact]
    public async Task Administrador_MantemAcessoOperacional_SemAssumirIdentidadeDeCliente()
    {
        using var factory = new ClienteJwtFactory();
        using var client = factory.CreateClient();
        var dados = await factory.SemearAsync();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { usuario = "admin", senha = "Admin@123" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        Autenticar(client, body.GetProperty("accessToken").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/clientes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/ordens-servico/{dados.OutraOrdem.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/minhas-ordens-servico")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/ordens-servico/{dados.Ordem.Id}/aprovar", null)).StatusCode);
    }

    private static void Autenticar(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task FalhaDoBancoNaValidacao_Retorna503SemDetalhesInternos()
    {
        using var factory = new ClienteJwtFactory { BancoIndisponivel = true };
        using var client = factory.CreateClient();
        Autenticar(client, factory.Token(Guid.NewGuid()));
        var response = await client.GetAsync("/api/minhas-ordens-servico");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("Npgsql", await response.Content.ReadAsStringAsync());
        Assert.Contains("temporariamente indisponivel", await response.Content.ReadAsStringAsync());
    }
}

internal sealed class ClienteJwtFactory : OficinaApiFactory
{
    private const string Issuer = "https://autenticacao.oficina.test";
    private readonly RSA _rsa = RSA.Create(2048);
    public bool BancoIndisponivel { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            if (BancoIndisponivel) services.AddScoped<IClienteRepository, ConsultaClienteIndisponivel>();
            services.Configure<ClienteJwtSettings>(options =>
            {
                options.Enabled = true; options.Issuer = Issuer; options.Audience = "oficina-api";
            });
            services.PostConfigure<JwtBearerOptions>(AutenticacaoExtensions.Cliente, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                configuration.SigningKeys.Add(new RsaSecurityKey(_rsa.ExportParameters(false)) { KeyId = "teste" });
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        });
    }

    public string Token(Guid clienteId, string? defeito = null)
    {
        if (defeito == "malformado") return "token-invalido";
        using var outraRsa = RSA.Create(2048);
        SecurityKey chave = new RsaSecurityKey(defeito == "assinatura" ? outraRsa : _rsa) { KeyId = "teste" };
        if (defeito == "algoritmo") chave = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)) { KeyId = "teste" };
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = defeito == "issuer" ? "https://outro.test" : Issuer,
            Audience = defeito == "audience" ? "outra-api" : "oficina-api",
            IssuedAt = DateTime.UtcNow.AddMinutes(-20), NotBefore = DateTime.UtcNow.AddMinutes(-20),
            Expires = defeito == "expirado" ? DateTime.UtcNow.AddMinutes(-2) : DateTime.UtcNow.AddMinutes(10),
            TokenType = defeito == "tipo" ? "JWT" : "at+jwt",
            SigningCredentials = new SigningCredentials(chave, defeito == "algoritmo" ? SecurityAlgorithms.HmacSha256 : SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = defeito == "sub" ? "nao-e-um-guid" : clienteId.ToString(),
                ["role"] = defeito == "role" ? "Admin" : "Cliente",
                ["scope"] = defeito == "scope" ? "outro:escopo" : "oficina:cliente"
            }
        });
    }

    public async Task<(Cliente Cliente, Cliente OutroCliente, OrdemServico Ordem, OrdemServico OutraOrdem, Servico Servico)> SemearAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OficinaDbContext>();
        var cliente = new Cliente("Cliente JWT", "52998224725", "11999999999", "cliente@example.test");
        var outro = new Cliente("Outro cliente", "12345678909", "11888888888", "outro@example.test");
        var veiculo = new Veiculo(cliente.Id, "JWT1234", "Fiat", "Uno", 2020);
        var outroVeiculo = new Veiculo(outro.Id, "OUT1234", "Fiat", "Uno", 2020);
        var servico = new Servico("Revisao", "Revisao de teste", 100, 30);
        var ordem = new OrdemServico(cliente.Id, veiculo.Id);
        var outraOrdem = new OrdemServico(outro.Id, outroVeiculo.Id);
        ordem.EnviarParaAprovacao(); outraOrdem.EnviarParaAprovacao();
        db.AddRange(cliente, outro, veiculo, outroVeiculo, servico, ordem, outraOrdem);
        await db.SaveChangesAsync();
        return (cliente, outro, ordem, outraOrdem, servico);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _rsa.Dispose();
    }
}

// Reproduz o encapsulamento observado no provider EF ao perder o PostgreSQL.
internal sealed class ConsultaClienteIndisponivel(OficinaDbContext db) : ClienteRepository(db), IClienteRepository
{
    Task<Cliente?> IClienteRepository.ObterPorIdAsync(Guid id) => throw new InvalidOperationException(
        "Falha transitoria", new Npgsql.NpgsqlException("Conexao indisponivel"));
}
