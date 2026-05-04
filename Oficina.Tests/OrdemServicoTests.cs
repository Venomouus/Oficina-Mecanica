using Oficina.Domain.Entities;
using Oficina.Domain.Enums;
using Xunit;

namespace Oficina.Tests;

public class OrdemServicoTests
{
    [Fact]
    public void EnviarParaAprovacao_DeveCalcularOrcamento()
    {
        var ordem = CriarOrdem();
        ordem.AdicionarServico(new OrdemServicoItem(Guid.NewGuid(), "Troca de oleo", 120, 60));
        ordem.AdicionarPeca(new OrdemServicoPeca(Guid.NewGuid(), "Filtro", 2, 45));

        ordem.EnviarParaAprovacao();

        Assert.Equal(StatusOrdemServico.AguardandoAprovacao, ordem.Status);
        Assert.Equal(210, ordem.ValorTotal);
    }

    [Fact]
    public void Aprovar_DeveMoverParaExecucao()
    {
        var ordem = CriarOrdem();
        ordem.EnviarParaAprovacao();

        ordem.Aprovar();

        Assert.Equal(StatusOrdemServico.EmExecucao, ordem.Status);
        Assert.NotNull(ordem.AprovadaEm);
        Assert.NotNull(ordem.IniciadaEm);
    }

    [Fact]
    public void Finalizar_DeveExigirExecucao()
    {
        var ordem = CriarOrdem();

        Assert.Throws<InvalidOperationException>(() => ordem.Finalizar());
    }

    [Fact]
    public void FluxoCompleto_DevePassarPorDiagnosticoExecucaoFinalizacaoEEntrega()
    {
        var ordem = CriarOrdem();

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
        var ordem = CriarOrdem();
        ordem.EnviarParaAprovacao();

        Assert.Throws<InvalidOperationException>(() => ordem.IniciarDiagnostico());
    }

    [Fact]
    public void IniciarExecucao_DeveExigirDiagnosticoOuAguardandoAprovacao()
    {
        var ordem = CriarOrdem();

        Assert.Throws<InvalidOperationException>(() => ordem.IniciarExecucao());
    }

    [Fact]
    public void Entregar_DeveExigirFinalizada()
    {
        var ordem = CriarOrdem();

        Assert.Throws<InvalidOperationException>(() => ordem.Entregar());
    }

    private static OrdemServico CriarOrdem()
    {
        return new OrdemServico(Guid.NewGuid(), Guid.NewGuid());
    }
}
