using Oficina.Domain.Entities;
using Xunit;

namespace Oficina.Tests;

public class PecaInsumoTests
{
    [Fact]
    public void BaixarEstoque_DeveReduzirQuantidade()
    {
        var peca = new PecaInsumo { Nome = "Filtro", QuantidadeEstoque = 5 };

        peca.BaixarEstoque(2);

        Assert.Equal(3, peca.QuantidadeEstoque);
    }

    [Fact]
    public void BaixarEstoque_DeveBloquearQuantidadeInsuficiente()
    {
        var peca = new PecaInsumo { Nome = "Filtro", QuantidadeEstoque = 1 };

        Assert.Throws<InvalidOperationException>(() => peca.BaixarEstoque(2));
    }

    [Fact]
    public void BaixarEstoque_DeveBloquearQuantidadeInvalida()
    {
        var peca = new PecaInsumo { Nome = "Filtro", QuantidadeEstoque = 1 };

        Assert.Throws<InvalidOperationException>(() => peca.BaixarEstoque(0));
    }

    [Fact]
    public void ReporEstoque_DeveSomarQuantidade()
    {
        var peca = new PecaInsumo { Nome = "Filtro", QuantidadeEstoque = 1 };

        peca.ReporEstoque(4);

        Assert.Equal(5, peca.QuantidadeEstoque);
    }

    [Fact]
    public void ReporEstoque_DeveBloquearQuantidadeInvalida()
    {
        var peca = new PecaInsumo { Nome = "Filtro", QuantidadeEstoque = 1 };

        Assert.Throws<InvalidOperationException>(() => peca.ReporEstoque(0));
    }
}
