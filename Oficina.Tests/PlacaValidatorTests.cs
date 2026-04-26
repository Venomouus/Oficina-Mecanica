using Oficina.Domain.Validation;
using Xunit;

namespace Oficina.Tests;

public class PlacaValidatorTests
{
    [Theory]
    [InlineData("ABC-1234")]
    [InlineData("BRA1E23")]
    public void IsValid_DeveAceitarPlacaAntigaEMercosul(string placa)
    {
        Assert.True(PlacaValidator.IsValid(placa));
    }

    [Theory]
    [InlineData("AB-1234")]
    [InlineData("ABC123")]
    [InlineData("")]
    public void IsValid_DeveRejeitarPlacasInvalidas(string placa)
    {
        Assert.False(PlacaValidator.IsValid(placa));
    }
}
