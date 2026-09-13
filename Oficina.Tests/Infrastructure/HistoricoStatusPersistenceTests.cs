using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Oficina.API.Contracts;
using Oficina.Application.Common;
using Oficina.Domain.Entities;
using Oficina.Domain.Enums;
using Oficina.Infrastructure.Persistence;
using Oficina.Infrastructure.Repositories;
using Xunit;

namespace Oficina.Tests.Infrastructure;

// SQLite exercita FKs, indices e transacoes; nao substitui a validacao da migration no PostgreSQL.
public class HistoricoStatusPersistenceTests
{
    [Fact]
    public async Task SalvarERecarregar_DevePreservarHistoricoEClienteInativo()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = Opcoes(connection);
        Guid id;
        await using (var db = new OficinaDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var ordem = await CriarOrdemAsync(db, clienteAtivo: false);
            id = ordem.Id;
        }
        await using (var db = new OficinaDbContext(options))
        {
            var repo = new OrdemServicoRepository(db);
            var ordem = (await repo.ObterDetalhadaAsync(id))!;
            Assert.False(ordem.Cliente!.Ativo);
            Assert.Equal(2, ordem.HistoricoStatus.Count);
            ordem.Aprovar();
            await repo.SalvarAlteracoesAsync();
        }
        await using (var db = new OficinaDbContext(options))
        {
            var ordem = (await new OrdemServicoRepository(db).ObterDetalhadaAsync(id))!;
            Assert.Equal(StatusOrdemServico.EmExecucao, ordem.Status);
            Assert.Equal(3, ordem.HistoricoStatus.Count);
            Assert.Single(ordem.HistoricoStatus, p => p.FinalizadaEm == null);
            Assert.All(ordem.HistoricoStatus, p => Assert.Equal(id, p.OrdemServicoId));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DuasDecisoesConcorrentes_DeveRejeitarSegundaSemHistoricoParcial(bool aprovar)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = Opcoes(connection);
        Guid id;
        await using (var seed = new OficinaDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            id = (await CriarOrdemAsync(seed)).Id;
        }
        await using var db1 = new OficinaDbContext(options);
        await using var db2 = new OficinaDbContext(options);
        var repo1 = new OrdemServicoRepository(db1);
        var repo2 = new OrdemServicoRepository(db2);
        var ordem1 = (await repo1.ObterDetalhadaAsync(id))!;
        var ordem2 = (await repo2.ObterDetalhadaAsync(id))!;
        if (aprovar)
        {
            ordem1.Aprovar();
            ordem2.Aprovar();
        }
        else
        {
            ordem1.RecusarOrcamento("Primeira resposta");
            ordem2.RecusarOrcamento("Segunda resposta");
        }
        await repo1.SalvarAlteracoesAsync();
        if (aprovar)
        {
            // SQLite insere o periodo antes do UPDATE com versao. A constraint deve
            // rejeitar a duplicacao e desfazer toda a transacao. A traducao de unique
            // violation para HTTP 409 no repositorio e especifica do PostgreSQL.
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => repo2.SalvarAlteracoesAsync());
            Assert.Equal(2067, Assert.IsType<SqliteException>(ex.InnerException).SqliteExtendedErrorCode);
        }
        else
        {
            await Assert.ThrowsAsync<ConflitoConcorrenciaException>(() => repo2.SalvarAlteracoesAsync());
        }

        await using var leitura = new OficinaDbContext(options);
        var persistida = (await new OrdemServicoRepository(leitura).ObterDetalhadaAsync(id))!;
        Assert.Equal(aprovar ? StatusOrdemServico.EmExecucao : StatusOrdemServico.AguardandoAprovacao, persistida.Status);
        Assert.Equal(ordem1.Versao, persistida.Versao);
        Assert.Equal(aprovar ? 3 : 2, persistida.HistoricoStatus.Count);
        Assert.Equal(ordem1.MotivoRecusaOrcamento, persistida.MotivoRecusaOrcamento);
        Assert.Single(persistida.HistoricoStatus, p => p.FinalizadaEm == null);
    }

    [Fact]
    public async Task PeriodoLegadoSemInicio_DevePermitirTransicaoSemInventarDuracao()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = Opcoes(connection);
        Guid id;
        await using (var seed = new OficinaDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            var ordem = await CriarOrdemAsync(seed);
            id = ordem.Id;
            // Emula uma OS anterior a migration: apenas estado atual, sem data de inicio.
            var inicial = ordem.HistoricoStatus.First();
            seed.HistoricosStatusOrdemServico.Remove(inicial);
            var atual = ordem.HistoricoStatus.Last();
            seed.Entry(atual).Property(p => p.Sequencia).CurrentValue = 1;
            seed.Entry(atual).Property(p => p.IniciadaEm).CurrentValue = null;
            await seed.SaveChangesAsync();
        }
        await using var db = new OficinaDbContext(options);
        var repo = new OrdemServicoRepository(db);
        var carregada = (await repo.ObterDetalhadaAsync(id))!;
        carregada.Aprovar();
        await repo.SalvarAlteracoesAsync();
        var legado = carregada.HistoricoStatus.Single(p => p.Sequencia == 1);
        Assert.NotNull(legado.FinalizadaEm);
        Assert.Null(HistoricoStatusResponse.FromEntity(legado).DuracaoMinutos);
        Assert.Equal(2, carregada.HistoricoStatus.Count);
    }

    private static DbContextOptions<OficinaDbContext> Opcoes(SqliteConnection connection) =>
        new DbContextOptionsBuilder<OficinaDbContext>().UseSqlite(connection).Options;

    private static async Task<OrdemServico> CriarOrdemAsync(OficinaDbContext db, bool clienteAtivo = true)
    {
        var cliente = new Cliente("Cliente", "12345678909", "11999999999", "cliente@example.test");
        cliente.AlterarStatus(clienteAtivo);
        var veiculo = new Veiculo(cliente.Id, "ABC1234", "Marca", "Modelo", 2020);
        var ordem = new OrdemServico(cliente.Id, veiculo.Id);
        ordem.EnviarParaAprovacao();
        db.AddRange(cliente, veiculo, ordem);
        await db.SaveChangesAsync();
        return ordem;
    }
}
