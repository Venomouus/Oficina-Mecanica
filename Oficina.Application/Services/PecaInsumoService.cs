using Oficina.Application.Common;
using Oficina.Application.Interfaces;
using Oficina.Domain.Entities;

namespace Oficina.Application.Services
{
    public class PecaInsumoService
    {
        private readonly IPecaInsumoRepository _pecas;

        public PecaInsumoService(IPecaInsumoRepository pecas)
        {
            _pecas = pecas;
        }

        public async Task<List<PecaInsumo>> ListarAsync()
        {
            return await _pecas.ListarAsync();
        }

        public async Task<PecaInsumo?> ObterPorIdAsync(Guid id)
        {
            return await _pecas.ObterPorIdAsync(id);
        }

        public async Task<ResultadoOperacao<PecaInsumo>> CriarAsync(
            string nome,
            string codigo,
            decimal precoUnitario,
            int quantidadeEstoque,
            int estoqueMinimo,
            bool ativo)
        {
            if (await _pecas.CodigoExisteAsync(codigo))
                return ResultadoOperacao<PecaInsumo>.Conflito("Ja existe uma peca/insumo com este codigo.");

            var peca = new PecaInsumo(nome, codigo, precoUnitario, quantidadeEstoque, estoqueMinimo);

            if (!ativo)
                peca.Desativar();

            await _pecas.AdicionarAsync(peca);
            await _pecas.SalvarAlteracoesAsync();

            return ResultadoOperacao<PecaInsumo>.Ok(peca);
        }

        public async Task<ResultadoOperacao> AtualizarAsync(
            Guid id,
            string nome,
            string codigo,
            decimal precoUnitario,
            int quantidadeEstoque,
            int estoqueMinimo,
            bool ativo)
        {
            var peca = await _pecas.ObterPorIdAsync(id);
            if (peca is null)
                return ResultadoOperacao.NaoEncontrado();

            if (await _pecas.CodigoExisteAsync(codigo, id))
                return ResultadoOperacao.Conflito("Ja existe outra peca/insumo com este codigo.");

            peca.Atualizar(nome, codigo, precoUnitario, estoqueMinimo);
            AjustarEstoque(peca, quantidadeEstoque);

            if (ativo)
                peca.Ativar();
            else
                peca.Desativar();

            await _pecas.SalvarAlteracoesAsync();
            return ResultadoOperacao.Ok();
        }

        public async Task<ResultadoOperacao<PecaInsumo>> ReporEstoqueAsync(Guid id, int quantidade)
        {
            var peca = await _pecas.ObterPorIdAsync(id);
            if (peca is null)
                return ResultadoOperacao<PecaInsumo>.NaoEncontrado();

            try
            {
                peca.ReporEstoque(quantidade);
                await _pecas.SalvarAlteracoesAsync();
                return ResultadoOperacao<PecaInsumo>.Ok(peca);
            }
            catch (InvalidOperationException ex)
            {
                return ResultadoOperacao<PecaInsumo>.DadosInvalidos(ex.Message);
            }
        }

        public async Task<ResultadoOperacao> RemoverAsync(Guid id)
        {
            var peca = await _pecas.ObterPorIdAsync(id);
            if (peca is null)
                return ResultadoOperacao.NaoEncontrado();

            _pecas.Remover(peca);
            await _pecas.SalvarAlteracoesAsync();
            return ResultadoOperacao.Ok();
        }

        private static void AjustarEstoque(PecaInsumo peca, int novaQuantidade)
        {
            var diferenca = novaQuantidade - peca.QuantidadeEstoque;

            if (diferenca > 0)
                peca.ReporEstoque(diferenca);
            else if (diferenca < 0)
                peca.BaixarEstoque(Math.Abs(diferenca));
        }
    }
}
