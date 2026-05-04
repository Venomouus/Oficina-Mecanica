using Oficina.Domain.Entities;

namespace Oficina.Application.Interfaces
{
    public interface IClienteRepository
    {
        Task<List<Cliente>> ListarAsync();
        Task<Cliente?> ObterPorIdAsync(Guid id);
        Task<Cliente?> ObterPorDocumentoAsync(string cpfCnpj);
        Task<bool> ExisteAsync(Guid id);
        Task AdicionarAsync(Cliente cliente);
        void Remover(Cliente cliente);
        Task SalvarAlteracoesAsync();
    }
}
