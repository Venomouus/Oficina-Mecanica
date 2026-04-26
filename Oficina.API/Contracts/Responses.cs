using Oficina.Domain.Entities;

namespace Oficina.API.Contracts;

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
        new { os.ClienteId, os.Cliente?.Nome, os.Cliente?.CpfCnpj },
        new { os.VeiculoId, os.Veiculo?.Placa, os.Veiculo?.Marca, os.Veiculo?.Modelo, os.Veiculo?.Ano },
        os.Servicos.Select(item => new { item.ServicoId, item.Nome, item.ValorUnitario, item.TempoEstimadoMinutos }),
        os.Pecas.Select(item => new { item.PecaInsumoId, item.Nome, item.Quantidade, item.ValorUnitario, Total = item.Quantidade * item.ValorUnitario }));
}
