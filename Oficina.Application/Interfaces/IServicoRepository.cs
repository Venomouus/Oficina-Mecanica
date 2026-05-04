using Oficina.Domain.Entities;

namespace Oficina.Application.Interfaces
{
    public interface IServicoRepository
    {
        Task<List<Servico>> ListarAsync();
        Task<Servico?> ObterPorIdAsync(Guid id);
        Task<List<Servico>> ListarAtivosPorIdsAsync(IEnumerable<Guid> ids);
        Task AdicionarAsync(Servico servico);
        void Remover(Servico servico);
        Task SalvarAlteracoesAsync();
    }
}
