using Oficina.Domain.Validation;
using Xunit;

namespace Oficina.Tests;

public class DocumentoValidatorTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("11.222.333/0001-81")]
    public void IsValid_DeveAceitarCpfECnpjValidos(string documento)
    {
        Assert.True(DocumentoValidator.IsValid(documento));
    }

    [Theory]
    [InlineData("111.111.111-11")]
    [InlineData("12.345.678/0001-00")]
    [InlineData("abc")]
    public void IsValid_DeveRejeitarDocumentosInvalidos(string documento)
    {
        Assert.False(DocumentoValidator.IsValid(documento));
    }

    [Fact]
    public void Normalize_DeveManterApenasDigitos()
    {
        Assert.Equal("52998224725", DocumentoValidator.Normalize("529.982.247-25"));
    }
}
