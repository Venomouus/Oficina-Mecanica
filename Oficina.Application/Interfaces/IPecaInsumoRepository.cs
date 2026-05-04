using Oficina.Domain.Entities;

namespace Oficina.Application.Interfaces
{
    public interface IPecaInsumoRepository
    {
        Task<List<PecaInsumo>> ListarAsync();
        Task<PecaInsumo?> ObterPorIdAsync(Guid id);
        Task<List<PecaInsumo>> ListarAtivasPorIdsAsync(IEnumerable<Guid> ids);
        Task<bool> CodigoExisteAsync(string codigo, Guid? ignorarId = null);
        Task AdicionarAsync(PecaInsumo peca);
        void Remover(PecaInsumo peca);
        Task SalvarAlteracoesAsync();
    }
}
