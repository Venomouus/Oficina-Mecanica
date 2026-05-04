using Oficina.Application.Interfaces;
using Oficina.Domain.Entities;

namespace Oficina.Application.Services
{
    public class VeiculoService
    {
        private readonly IVeiculoRepository _veiculos;
        private readonly IClienteRepository _clientes;

        public VeiculoService(IVeiculoRepository veiculos, IClienteRepository clientes)
        {
            _veiculos = veiculos;
            _clientes = clientes;
        }

        public async Task<List<Veiculo>> ListarAsync()
        {
            return await _veiculos.ListarAsync();
        }

        public async Task<Veiculo?> ObterPorIdAsync(Guid id)
        {
            return await _veiculos.ObterPorIdAsync(id);
        }

        public async Task<Veiculo> CriarAsync(Guid clienteId, string placa, string marca, string modelo, int ano)
        {
            var clienteExiste = await _clientes.ExisteAsync(clienteId);

            if (!clienteExiste)
                throw new Exception("Cliente nao encontrado.");

            var veiculo = new Veiculo(clienteId, placa, marca, modelo, ano);

            await _veiculos.AdicionarAsync(veiculo);
            await _veiculos.SalvarAlteracoesAsync();

            return veiculo;
        }

        public async Task AtualizarAsync(Guid id, string placa, string marca, string modelo, int ano)
        {
            var veiculo = await _veiculos.ObterPorIdAsync(id);

            if (veiculo == null)
                throw new Exception("Veiculo nao encontrado.");

            veiculo.Atualizar(placa, marca, modelo, ano);

            await _veiculos.SalvarAlteracoesAsync();
        }

        public async Task RemoverAsync(Guid id)
        {
            var veiculo = await _veiculos.ObterPorIdAsync(id);

            if (veiculo == null)
                throw new Exception("Veiculo nao encontrado.");

            if (await _veiculos.PossuiOrdensServicoAsync(id))
                throw new InvalidOperationException("Nao e possivel remover um veiculo que possui ordens de servico vinculadas.");

            _veiculos.Remover(veiculo);
            await _veiculos.SalvarAlteracoesAsync();
        }
    }
}
