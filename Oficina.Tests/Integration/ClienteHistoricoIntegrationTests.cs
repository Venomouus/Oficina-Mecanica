using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Oficina.Domain.Entities;
using Oficina.Infrastructure.Persistence;
using Xunit;

namespace Oficina.Tests.Integration;

public class ClienteHistoricoIntegrationTests
{
    [Fact]
    public async Task StatusCliente_DeveExigirAdminEPermitirDesativarReativarSemExcluirRelacionamentos()
    {
        using var factory = new OficinaApiFactory();
        using var client = factory.CreateClient();
        Guid id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OficinaDbContext>();
            var cliente = new Cliente("Cliente", "12345678909", "11999999999", "cliente@example.test");
            id = cliente.Id;
            db.AddRange(cliente, new Veiculo(id, "ABC1234", "Marca", "Modelo", 2020));
            await db.SaveChangesAsync();
        }
        var url = $"/api/clientes/{id}/status";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PatchAsJsonAsync(url, new { ativo = false })).StatusCode);
        await AutenticarAsync(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync(url, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PatchAsJsonAsync($"/api/clientes/{Guid.NewGuid()}/status", new { ativo = false })).StatusCode);
        foreach (var ativo in new[] { false, true })
        {
            var resposta = await client.PatchAsJsonAsync(url, new { ativo });
            resposta.EnsureSuccessStatusCode();
            var salvo = await client.GetFromJsonAsync<JsonElement>($"/api/clientes/{id}");
            Assert.Equal(ativo, salvo.GetProperty("ativo").GetBoolean());
            Assert.Single(salvo.GetProperty("veiculos").EnumerateArray());
        }
    }

    [Fact]
    public async Task Historico_DeveManterCriacaoEmAprovacaoERegistrarTransicoesDaApi()
    {
        using var factory = new OficinaApiFactory();
        using var client = factory.CreateClient();
        await AutenticarAsync(client);
        var servico = await client.PostAsJsonAsync("/api/servicos", new
        {
            nome = "Revisao", descricao = "Revisao teste", preco = 100, tempoEstimadoMinutos = 60, ativo = true
        });
        servico.EnsureSuccessStatusCode();
        var servicoBody = await servico.Content.ReadFromJsonAsync<JsonElement>();
        var resposta = await client.PostAsJsonAsync("/api/ordens-servico", new
        {
            cliente = new { nome = "Cliente", cpfCnpj = "12345678909", telefone = "11999999999", email = "cliente@example.test" },
            veiculo = new { placa = "ABC1234", marca = "Marca", modelo = "Modelo", ano = 2020 },
            servicosIds = new[] { servicoBody.GetProperty("id").GetGuid() }, pecas = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var body = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Aguardando Aprovacao", body.GetProperty("status").GetString());
        var id = body.GetProperty("id").GetGuid();
        foreach (var status in new[] { 4, 5, 6 })
            (await client.PatchAsJsonAsync($"/api/ordens-servico/{id}/status", new { status })).EnsureSuccessStatusCode();

        var periodos = await client.GetFromJsonAsync<JsonElement[]>($"/api/ordens-servico/{id}/historico");
        Assert.NotNull(periodos);
        Assert.Equal(new[] { "Recebida", "Aguardando Aprovacao", "Execucao", "Finalizada", "Entregue" },
            periodos.Select(p => p.GetProperty("status").GetString()));
        Assert.Equal(JsonValueKind.Null, periodos.Last().GetProperty("duracaoMinutos").ValueKind);
        Assert.All(periodos.Take(4), p => Assert.True(p.GetProperty("duracaoMinutos").GetDouble() >= 0));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/ordens-servico/{Guid.NewGuid()}/historico")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/ordens-servico/{id}/historico")).StatusCode);
    }

    private static async Task AutenticarAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { usuario = "admin", senha = "Admin@123" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
    }
}
