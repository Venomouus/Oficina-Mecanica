using Oficina.Application.Models;
using Oficina.Domain.Entities;

namespace Oficina.Application.Interfaces
{
    public interface IOrdemServicoRepository
    {
        Task<List<OrdemServicoResumo>> ListarResumoAsync();
        Task<List<OrdemServicoResumo>> ListarFilaOperacionalAsync();
        Task<OrdemServico?> ObterDetalhadaAsync(Guid id);
        Task AdicionarAsync(OrdemServico ordem);
        Task<TempoMedioExecucao> CalcularTempoMedioExecucaoAsync();
        Task SalvarAlteracoesAsync();
    }
}