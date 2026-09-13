using Oficina.Application.Interfaces;
using Oficina.Application.Common;
using Oficina.Domain.Entities;
using Oficina.Domain.Validation;

namespace Oficina.Application.Services
{
    public class ClienteService
    {
        private readonly IClienteRepository _clientes;

        public ClienteService(IClienteRepository clientes)
        {
            _clientes = clientes;
        }

        public async Task<List<Cliente>> ListarAsync()
        {
            return await _clientes.ListarAsync();
        }

        public async Task<Cliente?> ObterPorIdAsync(Guid id)
        {
            return await _clientes.ObterPorIdAsync(id);
        }

        public async Task<Cliente> CriarAsync(string nome, string cpfCnpj, string telefone, string email)
        {
            if (!DocumentoValidator.IsValid(cpfCnpj))
                throw new ArgumentException("CPF/CNPJ invalido.");

            var documento = DocumentoValidator.Normalize(cpfCnpj);
            var cliente = new Cliente(nome, documento, telefone, email);

            await _clientes.AdicionarAsync(cliente);
            await _clientes.SalvarAlteracoesAsync();

            return cliente;
        }

        public async Task AtualizarAsync(Guid id, string nome, string telefone, string email)
        {
            var cliente = await _clientes.ObterPorIdAsync(id);

            if (cliente == null)
                throw new Exception("Cliente nao encontrado.");

            cliente.Atualizar(nome, telefone, email);

            await _clientes.SalvarAlteracoesAsync();
        }

        public async Task<ResultadoOperacao<Cliente>> AlterarStatusAsync(Guid id, bool ativo)
        {
            var cliente = await _clientes.ObterPorIdAsync(id);
            if (cliente is null)
                return ResultadoOperacao<Cliente>.NaoEncontrado();

            cliente.AlterarStatus(ativo);
            await _clientes.SalvarAlteracoesAsync();
            return ResultadoOperacao<Cliente>.Ok(cliente);
        }

        public async Task RemoverAsync(Guid id)
        {
            var cliente = await _clientes.ObterPorIdAsync(id);

            if (cliente == null)
                throw new Exception("Cliente nao encontrado.");

            if (await _clientes.PossuiVinculosAsync(id))
                throw new InvalidOperationException("Nao e possivel remover um cliente que possui veiculos ou ordens de servico vinculadas.");

            _clientes.Remover(cliente);
            await _clientes.SalvarAlteracoesAsync();
        }
    }
}
