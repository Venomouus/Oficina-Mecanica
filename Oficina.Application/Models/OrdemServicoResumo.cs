using Oficina.Domain.Enums;

namespace Oficina.Application.Models
{
    public record OrdemServicoResumo(
        Guid Id,
        string Numero,
        StatusOrdemServico Status,
        decimal ValorTotal,
        DateTime CriadaEm);
}
