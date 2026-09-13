using Oficina.API.Contracts;
using Oficina.Domain.Entities;
using Oficina.Domain.Enums;
using Xunit;

namespace Oficina.Tests;

public class HistoricoStatusTests
{
    [Fact]
    public void FluxoCompleto_DeveRegistrarPeriodosContiguosComDuracoesReais()
    {
        var relogio = new RelogioTeste();
        var ordem = new OrdemServico(Guid.NewGuid(), Guid.NewGuid(), timeProvider: relogio);
        relogio.Avancar(5);
        ordem.IniciarDiagnostico();
        relogio.Avancar(20);
        ordem.EnviarParaAprovacao();
        relogio.Avancar(10);
        ordem.Aprovar();
        relogio.Avancar(60);
        ordem.Finalizar();
        relogio.Avancar(15);
        ordem.Entregar();

        var periodos = ordem.HistoricoStatus.ToArray();
        Assert.Equal(new[] { StatusOrdemServico.Recebida, StatusOrdemServico.EmDiagnostico,
            StatusOrdemServico.AguardandoAprovacao, StatusOrdemServico.EmExecucao,
            StatusOrdemServico.Finalizada, StatusOrdemServico.Entregue }, periodos.Select(p => p.Status));
        Assert.Equal(new double?[] { 5, 20, 10, 60, 15, null },
            periodos.Select(p => HistoricoStatusResponse.FromEntity(p).DuracaoMinutos));
        Assert.Equal(Enumerable.Range(1, 6), periodos.Select(p => p.Sequencia));
        for (var i = 1; i < periodos.Length; i++)
            Assert.Equal(periodos[i - 1].FinalizadaEm, periodos[i].IniciadaEm);
        Assert.Equal(ordem.IniciadaEm, periodos[3].IniciadaEm);
        Assert.Equal(ordem.FinalizadaEm, periodos[4].IniciadaEm);
        Assert.Single(periodos, p => !p.FinalizadaEm.HasValue);
    }

    [Fact]
    public void CriacaoDiretaEmAprovacao_NaoDeveInventarDiagnostico()
    {
        var ordem = new OrdemServico(Guid.NewGuid(), Guid.NewGuid());
        ordem.EnviarParaAprovacao();
        Assert.Equal(StatusOrdemServico.AguardandoAprovacao, ordem.Status);
        Assert.Equal(2, ordem.HistoricoStatus.Count);
        Assert.DoesNotContain(ordem.HistoricoStatus, p => p.Status == StatusOrdemServico.EmDiagnostico);
    }

    [Fact]
    public void RecusaEReenvio_NaoDevemDuplicarPeriodoDeAprovacao()
    {
        var relogio = new RelogioTeste();
        var ordem = new OrdemServico(Guid.NewGuid(), Guid.NewGuid(), timeProvider: relogio);
        ordem.EnviarParaAprovacao();
        relogio.Avancar(10);
        ordem.RecusarOrcamento("Valor alto");
        Assert.Throws<InvalidOperationException>(() => ordem.IniciarExecucao());
        relogio.Avancar(20);
        ordem.EnviarParaAprovacao();
        Assert.Equal(2, ordem.HistoricoStatus.Count);
        Assert.Null(ordem.OrcamentoAprovado);
        ordem.Aprovar();
        Assert.Equal(30d, HistoricoStatusResponse.FromEntity(ordem.HistoricoStatus.ElementAt(1)).DuracaoMinutos);
    }

    [Fact]
    public void TransicaoInvalida_NaoDeveAlterarHistoricoNemVersao()
    {
        var ordem = new OrdemServico(Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => ordem.Finalizar());
        Assert.Equal(0, ordem.Versao);
        Assert.Null(Assert.Single(ordem.HistoricoStatus).FinalizadaEm);
        ordem.EnviarParaAprovacao();
        ordem.Aprovar();
        ordem.Finalizar();
        var versao = ordem.Versao;
        Assert.Throws<InvalidOperationException>(() => ordem.EnviarParaAprovacao());
        Assert.Equal(versao, ordem.Versao);
        Assert.Equal(StatusOrdemServico.Finalizada, ordem.Status);
    }

    [Fact]
    public void RelogioRetrocedendo_NaoDeveCriarDuracaoNegativa()
    {
        var relogio = new RelogioTeste();
        var ordem = new OrdemServico(Guid.NewGuid(), Guid.NewGuid(), timeProvider: relogio);
        relogio.Avancar(-1);
        Assert.Throws<InvalidOperationException>(() => ordem.EnviarParaAprovacao());
        Assert.Equal(StatusOrdemServico.Recebida, ordem.Status);
        Assert.Null(Assert.Single(ordem.HistoricoStatus).FinalizadaEm);
    }

    private sealed class RelogioTeste : TimeProvider
    {
        private DateTimeOffset _agora = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _agora;
        public void Avancar(int minutos) => _agora = _agora.AddMinutes(minutos);
    }
}
