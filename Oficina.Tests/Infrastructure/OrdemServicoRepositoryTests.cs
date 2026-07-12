using Microsoft.EntityFrameworkCore;
using Oficina.Domain.Entities;
using Oficina.Domain.Enums;
using Oficina.Infrastructure.Persistence;
using Oficina.Infrastructure.Repositories;
using Xunit;

namespace Oficina.Tests.Infrastructure;

public class OrdemServicoRepositoryTests
{
    [Fact]
    public async Task ListarFilaOperacional_DeveOrdenarPorPrioridadeEIgnorarOrdensEncerradas()
    {
        var options = new DbContextOptionsBuilder<OficinaDbContext>()
            .UseInMemoryDatabase($"fila-operacional-{Guid.NewGuid()}")
            .Options;

        await using var context = new OficinaDbContext(options);

        var execucaoAntiga = CriarOrdem(StatusOrdemServico.EmExecucao);
        var execucaoNova = CriarOrdem(StatusOrdemServico.EmExecucao);
        var aguardandoAprovacao = CriarOrdem(StatusOrdemServico.AguardandoAprovacao);
        var diagnostico = CriarOrdem(StatusOrdemServico.EmDiagnostico);
        var recebida = CriarOrdem(StatusOrdemServico.Recebida);
        var finalizada = CriarOrdem(StatusOrdemServico.Finalizada);
        var entregue = CriarOrdem(StatusOrdemServico.Entregue);

        context.OrdensServico.AddRange(
            recebida,
            entregue,
            diagnostico,
            execucaoNova,
            finalizada,
            aguardandoAprovacao,
            execucaoAntiga);

        DefinirCriacao(context, execucaoAntiga, DateTime.UtcNow.AddHours(-7));
        DefinirCriacao(context, execucaoNova, DateTime.UtcNow.AddHours(-2));
        DefinirCriacao(context, aguardandoAprovacao, DateTime.UtcNow.AddHours(-6));
        DefinirCriacao(context, diagnostico, DateTime.UtcNow.AddHours(-5));
        DefinirCriacao(context, recebida, DateTime.UtcNow.AddHours(-4));
        DefinirCriacao(context, finalizada, DateTime.UtcNow.AddHours(-8));
        DefinirCriacao(context, entregue, DateTime.UtcNow.AddHours(-9));

        await context.SaveChangesAsync();

        var repository = new OrdemServicoRepository(context);

        var fila = await repository.ListarFilaOperacionalAsync();

        Assert.Equal(
            new[]
            {
                execucaoAntiga.Id,
                execucaoNova.Id,
                aguardandoAprovacao.Id,
                diagnostico.Id,
                recebida.Id
            },
            fila.Select(ordem => ordem.Id));

        Assert.DoesNotContain(fila, ordem => ordem.Id == finalizada.Id);
        Assert.DoesNotContain(fila, ordem => ordem.Id == entregue.Id);
    }

    private static OrdemServico CriarOrdem(StatusOrdemServico status)
    {
        var ordem = new OrdemServico(Guid.NewGuid(), Guid.NewGuid());

        switch (status)
        {
            case StatusOrdemServico.EmDiagnostico:
                ordem.IniciarDiagnostico();
                break;
            case StatusOrdemServico.AguardandoAprovacao:
                ordem.EnviarParaAprovacao();
                break;
            case StatusOrdemServico.EmExecucao:
                ordem.EnviarParaAprovacao();
                ordem.Aprovar();
                break;
            case StatusOrdemServico.Finalizada:
                ordem.EnviarParaAprovacao();
                ordem.Aprovar();
                ordem.Finalizar();
                break;
            case StatusOrdemServico.Entregue:
                ordem.EnviarParaAprovacao();
                ordem.Aprovar();
                ordem.Finalizar();
                ordem.Entregar();
                break;
        }

        return ordem;
    }

    private static void DefinirCriacao(OficinaDbContext context, OrdemServico ordem, DateTime criadaEm)
    {
        context.Entry(ordem)
            .Property(nameof(OrdemServico.CriadaEm))
            .CurrentValue = criadaEm;
    }
}