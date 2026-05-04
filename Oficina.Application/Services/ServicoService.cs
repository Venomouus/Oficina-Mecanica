using Oficina.Application.Interfaces;
using Oficina.Domain.Entities;

namespace Oficina.Application.Services
{
    public class ServicoService
    {
        private readonly IServicoRepository _servicos;

        public ServicoService(IServicoRepository servicos)
        {
            _servicos = servicos;
        }

        public async Task<List<Servico>> ListarAsync()
        {
            return await _servicos.ListarAsync();
        }

        public async Task<Servico?> ObterPorIdAsync(Guid id)
        {
            return await _servicos.ObterPorIdAsync(id);
        }

        public async Task<Servico> CriarAsync(string nome, string descricao, decimal preco, int tempoEstimadoMinutos)
        {
            var servico = new Servico(nome, descricao, preco, tempoEstimadoMinutos);

            await _servicos.AdicionarAsync(servico);
            await _servicos.SalvarAlteracoesAsync();

            return servico;
        }

        public async Task AtualizarAsync(Guid id, string nome, string descricao, decimal preco, int tempoEstimadoMinutos)
        {
            var servico = await _servicos.ObterPorIdAsync(id);

            if (servico == null)
                throw new Exception("Servico nao encontrado.");

            servico.Atualizar(nome, descricao, preco, tempoEstimadoMinutos);

            await _servicos.SalvarAlteracoesAsync();
        }

        public async Task RemoverAsync(Guid id)
        {
            var servico = await _servicos.ObterPorIdAsync(id);

            if (servico == null)
                throw new Exception("Servico nao encontrado.");

            _servicos.Remover(servico);
            await _servicos.SalvarAlteracoesAsync();
        }
    }
}
