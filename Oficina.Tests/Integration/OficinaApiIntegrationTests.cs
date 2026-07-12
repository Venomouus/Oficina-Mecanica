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
    public async Task CriarOrdemServico_ComClienteVeiculoServicosEPecas_DeveRetornarCreated()
    {
        await AutenticarAsync();

        var servicoId = await CriarServicoAsync();
        var pecaId = await CriarPecaAsync();

        var response = await _client.PostAsJsonAsync("/api/ordens-servico", new
        {
            cliente = new
            {
                nome = "Cliente Integracao",
                cpfCnpj = "123.456.789-09",
                telefone = "11999999999",
                email = "integracao@teste.com"
            },
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
        Assert.Equal("Aguardando Aprovacao", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(160, body.RootElement.GetProperty("valorTotal").GetDecimal());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("numero").GetString()));
    }

    [Fact]
    public async Task ConsultarStatus_DeveRetornarSituacaoAtualDaOrdem()
    {
        var ordem = await CriarOrdemCompletaAsync("ABC1D23");

        var response = await _client.GetAsync($"/api/ordens-servico/{ordem.Id}/status?cpfCnpj=123.456.789-09");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(ordem.Id, body.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Aguardando Aprovacao", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task NotificacaoExterna_DeveRegistrarRecusaDoOrcamento()
    {
        var ordem = await CriarOrdemCompletaAsync("REC1D23");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ordens-servico/orcamentos/notificacoes")
        {
            Content = JsonContent.Create(new
            {
                ordemServicoId = ordem.Id,
                decisao = "recusado",
                cpfCnpjCliente = "123.456.789-09",
                motivo = "Cliente vai avaliar depois"
            })
        };
        request.Headers.Add("X-Webhook-Token", "token-dev-orcamento");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("Aguardando Aprovacao", body.RootElement.GetProperty("status").GetString());
        Assert.False(body.RootElement.GetProperty("orcamentoAprovado").GetBoolean());
        Assert.Equal("Cliente vai avaliar depois", body.RootElement.GetProperty("motivoRecusaOrcamento").GetString());
    }

    [Fact]
    public async Task NotificacaoExterna_Aprovada_DeveBaixarEstoqueDasPecas()
    {
        var ordem = await CriarOrdemCompletaAsync("APR1D23", quantidadePeca: 2);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ordens-servico/orcamentos/notificacoes")
        {
            Content = JsonContent.Create(new
            {
                ordemServicoId = ordem.Id,
                decisao = "aprovado",
                cpfCnpjCliente = "123.456.789-09"
            })
        };
        request.Headers.Add("X-Webhook-Token", "token-dev-orcamento");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var ordemBody = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("Execucao", ordemBody.RootElement.GetProperty("status").GetString());
        Assert.True(ordemBody.RootElement.GetProperty("orcamentoAprovado").GetBoolean());

        var peca = await _client.GetAsync($"/api/pecas-insumos/{ordem.PecaId}");
        peca.EnsureSuccessStatusCode();

        using var pecaBody = await JsonDocument.ParseAsync(await peca.Content.ReadAsStreamAsync());
        Assert.Equal(3, pecaBody.RootElement.GetProperty("quantidadeEstoque").GetInt32());
    }

    private async Task AutenticarAsync()
    {
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
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

    private async Task<(Guid Id, Guid PecaId)> CriarOrdemCompletaAsync(string placa, int quantidadePeca = 1)
    {
        await AutenticarAsync();

        var servicoId = await CriarServicoAsync();
        var pecaId = await CriarPecaAsync();

        var response = await _client.PostAsJsonAsync("/api/ordens-servico", new
        {
            cliente = new
            {
                nome = "Cliente Integracao",
                cpfCnpj = "123.456.789-09",
                telefone = "11999999999",
                email = "integracao@teste.com"
            },
            veiculo = new
            {
                placa,
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
                    quantidade = quantidadePeca
                }
            },
            observacoes = "Teste de integracao"
        });

        response.EnsureSuccessStatusCode();

        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return (body.RootElement.GetProperty("id").GetGuid(), pecaId);
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