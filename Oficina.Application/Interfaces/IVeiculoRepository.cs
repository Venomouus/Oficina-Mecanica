using Oficina.Domain.Entities;

namespace Oficina.Application.Interfaces
{
    public interface IVeiculoRepository
    {
        Task<List<Veiculo>> ListarAsync();
        Task<Veiculo?> ObterPorIdAsync(Guid id);
        Task<Veiculo?> ObterPorPlacaAsync(string placa);
        Task AdicionarAsync(Veiculo veiculo);
        void Remover(Veiculo veiculo);
        Task SalvarAlteracoesAsync();
    }
}
