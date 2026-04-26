using Oficina.Domain.Entities;
using Oficina.Domain.Enums;
using Xunit;

namespace Oficina.Tests;

public class OrdemServicoTests
{
    [Fact]
    public void EnviarParaAprovacao_DeveCalcularOrcamento()
    {
        var ordem = new OrdemServico();
        ordem.Servicos.Add(new OrdemServicoServico { Nome = "Troca de oleo", ValorUnitario = 120 });
        ordem.Pecas.Add(new OrdemServicoPeca { Nome = "Filtro", Quantidade = 2, ValorUnitario = 45 });

        ordem.EnviarParaAprovacao();

        Assert.Equal(StatusOrdemServico.AguardandoAprovacao, ordem.Status);
        Assert.Equal(210, ordem.ValorTotal);
    }

    [Fact]
    public void Aprovar_DeveMoverParaExecucao()
    {
        var ordem = new OrdemServico();
        ordem.EnviarParaAprovacao();

        ordem.Aprovar();

        Assert.Equal(StatusOrdemServico.EmExecucao, ordem.Status);
        Assert.NotNull(ordem.AprovadaEm);
        Assert.NotNull(ordem.IniciadaEm);
    }

    [Fact]
    public void Finalizar_DeveExigirExecucao()
    {
        var ordem = new OrdemServico();

        Assert.Throws<InvalidOperationException>(() => ordem.Finalizar());
    }

    [Fact]
    public void FluxoCompleto_DevePassarPorDiagnosticoExecucaoFinalizacaoEEntrega()
    {
        var ordem = new OrdemServico();

        ordem.IniciarDiagnostico();
        ordem.IniciarExecucao();
        ordem.Finalizar();
        ordem.Entregar();

        Assert.Equal(StatusOrdemServico.Entregue, ordem.Status);
        Assert.NotNull(ordem.IniciadaEm);
        Assert.NotNull(ordem.FinalizadaEm);
        Assert.NotNull(ordem.EntregueEm);
    }

    [Fact]
    public void IniciarDiagnostico_DeveExigirStatusRecebida()
    {
        var ordem = new OrdemServico();
        ordem.EnviarParaAprovacao();

        Assert.Throws<InvalidOperationException>(() => ordem.IniciarDiagnostico());
    }

    [Fact]
    public void IniciarExecucao_DeveExigirDiagnosticoOuAguardandoAprovacao()
    {
        var ordem = new OrdemServico();

        Assert.Throws<InvalidOperationException>(() => ordem.IniciarExecucao());
    }

    [Fact]
    public void Entregar_DeveExigirFinalizada()
    {
        var ordem = new OrdemServico();

        Assert.Throws<InvalidOperationException>(() => ordem.Entregar());
    }
}
