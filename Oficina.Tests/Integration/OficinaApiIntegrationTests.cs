using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Oficina.Tests.Integration;

public class OficinaApiIntegrationTests : IClassFixture<OficinaApiFactory>
{
    private readonly HttpClient _client;

    public OficinaApiIntegrationTests(OficinaApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_DeveRetornarTokenJwt()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            usuario = "admin",
            senha = "Admin@123"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var token = body.RootElement.GetProperty("accessToken").GetString();
        var tokenType = body.RootElement.GetProperty("tokenType").GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal("Bearer", tokenType);
    }

    [Fact]
    public async Task CriarOrdemServico_ComDadosValidos_DeveRetornarCreated()
    {
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var clienteId = await CriarClienteAsync();
        var servicoId = await CriarServicoAsync();
        var pecaId = await CriarPecaAsync();

        var response = await _client.PostAsJsonAsync("/api/ordens-servico", new
        {
            cpfCnpjCliente = "123.456.789-09",
            veiculo = new
            {
                placa = "DDD1234",
                marca = "Renault",
                modelo = "Kwid",
                ano = 2026
            },
            servicosIds = new[] { servicoId },
            pecas = new[]
            {
                new
                {
                    pecaInsumoId = pecaId,
                    quantidade = 1
                }
            },
            observacoes = "Teste de integracao"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("AguardandoAprovacao", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(160, body.RootElement.GetProperty("valorTotal").GetDecimal());

        _ = clienteId;
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            usuario = "admin",
            senha = "Admin@123"
        });

        response.EnsureSuccessStatusCode();

        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }

    private async Task<Guid> CriarClienteAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/clientes", new
        {
            nome = "Cliente Integracao",
            cpfCnpj = "123.456.789-09",
            telefone = "11999999999",
            email = "integracao@teste.com"
        });

        response.EnsureSuccessStatusCode();

        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CriarServicoAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/servicos", new
        {
            nome = "Troca de oleo",
            descricao = "Servico usado no teste de integracao",
            preco = 100,
            tempoEstimadoMinutos = 60,
            ativo = true
        });

        response.EnsureSuccessStatusCode();

        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CriarPecaAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/pecas-insumos", new
        {
            nome = "Filtro de oleo",
            codigo = $"FILTRO-{Guid.NewGuid():N}",
            precoUnitario = 60,
            quantidadeEstoque = 5,
            estoqueMinimo = 1,
            ativo = true
        });

        response.EnsureSuccessStatusCode();

        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }
}
