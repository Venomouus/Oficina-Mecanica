using Oficina.Domain.Entities;
using Xunit;

namespace Oficina.Tests;

public class PecaInsumoTests
{
    [Fact]
    public void BaixarEstoque_DeveReduzirQuantidade()
    {
        var peca = CriarPeca(5);

        peca.BaixarEstoque(2);

        Assert.Equal(3, peca.QuantidadeEstoque);
    }

    [Fact]
    public void BaixarEstoque_DeveBloquearQuantidadeInsuficiente()
    {
        var peca = CriarPeca(1);

        Assert.Throws<InvalidOperationException>(() => peca.BaixarEstoque(2));
    }

    [Fact]
    public void BaixarEstoque_DeveBloquearQuantidadeInvalida()
    {
        var peca = CriarPeca(1);

        Assert.Throws<InvalidOperationException>(() => peca.BaixarEstoque(0));
    }

    [Fact]
    public void ReporEstoque_DeveSomarQuantidade()
    {
        var peca = CriarPeca(1);

        peca.ReporEstoque(4);

        Assert.Equal(5, peca.QuantidadeEstoque);
    }

    [Fact]
    public void ReporEstoque_DeveBloquearQuantidadeInvalida()
    {
        var peca = CriarPeca(1);

        Assert.Throws<InvalidOperationException>(() => peca.ReporEstoque(0));
    }

    private static PecaInsumo CriarPeca(int quantidadeEstoque)
    {
        return new PecaInsumo("Filtro", "FILTRO", 10, quantidadeEstoque, 0);
    }
}
