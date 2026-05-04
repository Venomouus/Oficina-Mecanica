using Oficina.Domain.Entities;
using Oficina.Domain.Validation;

namespace Oficina.API.Contracts;

public record ClienteResponse(
    Guid Id,
    string Nome,
    string CpfCnpj,
    string Telefone,
    string Email,
    IEnumerable<object> Veiculos)
{
    public static ClienteResponse FromEntity(Cliente cliente) => new(
        cliente.Id,
        cliente.Nome,
        DocumentoFormatter.Format(cliente.CpfCnpj),
        cliente.Telefone,
        cliente.Email,
        cliente.Veiculos.Select(veiculo => new
        {
            veiculo.Id,
            veiculo.Placa,
            veiculo.Marca,
            veiculo.Modelo,
            veiculo.Ano,
            veiculo.ClienteId
        }));
}

public record OrdemServicoResumoResponse(Guid Id, string Numero, string Status, decimal ValorTotal, DateTime CriadaEm);

public record OrdemServicoDetalheResponse(
    Guid Id,
    string Numero,
    string Status,
    decimal ValorTotal,
    DateTime CriadaEm,
    DateTime? AprovadaEm,
    DateTime? IniciadaEm,
    DateTime? FinalizadaEm,
    DateTime? EntregueEm,
    object Cliente,
    object Veiculo,
    IEnumerable<object> Servicos,
    IEnumerable<object> Pecas)
{
    public static OrdemServicoDetalheResponse FromEntity(OrdemServico os) => new(
        os.Id,
        os.Numero,
        os.Status.ToString(),
        os.ValorTotal,
        os.CriadaEm,
        os.AprovadaEm,
        os.IniciadaEm,
        os.FinalizadaEm,
        os.EntregueEm,
        new { os.ClienteId, os.Cliente?.Nome, CpfCnpj = DocumentoFormatter.Format(os.Cliente?.CpfCnpj) },
        new { os.VeiculoId, os.Veiculo?.Placa, os.Veiculo?.Marca, os.Veiculo?.Modelo, os.Veiculo?.Ano },
        os.Servicos.Select(item => new { item.ServicoId, item.Nome, item.ValorUnitario, item.TempoEstimadoMinutos }),
        os.Pecas.Select(item => new { item.PecaInsumoId, item.Nome, item.Quantidade, item.ValorUnitario, Total = item.Quantidade * item.ValorUnitario }));
}

public static class DocumentoFormatter
{
    public static string Format(string? documento)
    {
        if (string.IsNullOrWhiteSpace(documento))
            return string.Empty;

        var digits = DocumentoValidator.Normalize(documento);

        return digits.Length switch
        {
            11 => $"{digits[..3]}.{digits.Substring(3, 3)}.{digits.Substring(6, 3)}-{digits[9..]}",
            14 => $"{digits[..2]}.{digits.Substring(2, 3)}.{digits.Substring(5, 3)}/{digits.Substring(8, 4)}-{digits[12..]}",
            _ => documento
        };
    }
}
