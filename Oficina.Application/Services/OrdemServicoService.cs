using Oficina.Application.Common;
using Oficina.Application.Interfaces;
using Oficina.Application.Models;
using Oficina.Domain.Entities;
using Oficina.Domain.Enums;
using Oficina.Domain.Validation;

namespace Oficina.Application.Services
{
    public class OrdemServicoService
    {
        private readonly IClienteRepository _clientes;
        private readonly IVeiculoRepository _veiculos;
        private readonly IServicoRepository _servicos;
        private readonly IPecaInsumoRepository _pecas;
        private readonly IOrdemServicoRepository _ordens;

        public OrdemServicoService(
            IClienteRepository clientes,
            IVeiculoRepository veiculos,
            IServicoRepository servicos,
            IPecaInsumoRepository pecas,
            IOrdemServicoRepository ordens)
        {
            _clientes = clientes;
            _veiculos = veiculos;
            _servicos = servicos;
            _pecas = pecas;
            _ordens = ordens;
        }

        public async Task<List<OrdemServicoResumo>> ListarResumoAsync()
        {
            return await _ordens.ListarResumoAsync();
        }

        public async Task<OrdemServico?> ObterDetalhadaAsync(Guid id)
        {
            return await _ordens.ObterDetalhadaAsync(id);
        }

        public async Task<ResultadoOperacao<OrdemServico>> CriarAsync(
            string cpfCnpjCliente,
            VeiculoOrdemInput veiculoInput,
            List<Guid> servicosIds,
            List<PecaOrdemInput> pecasInput,
            string? observacoes)
        {
            if (!DocumentoValidator.IsValid(cpfCnpjCliente))
                return ResultadoOperacao<OrdemServico>.DadosInvalidos("CPF/CNPJ invalido.");

            if (servicosIds.Count == 0)
                return ResultadoOperacao<OrdemServico>.DadosInvalidos("Informe pelo menos um servico.");

            var documento = DocumentoValidator.Normalize(cpfCnpjCliente);
            var cliente = await _clientes.ObterPorDocumentoAsync(documento);
            if (cliente is null)
                return ResultadoOperacao<OrdemServico>.NaoEncontrado("Cliente nao encontrado.");

            var veiculo = await ObterOuCriarVeiculoAsync(veiculoInput, cliente.Id);
            if (veiculo is null)
                return ResultadoOperacao<OrdemServico>.DadosInvalidos("Dados do veiculo invalidos ou vinculados a outro cliente.");

            var servicos = await _servicos.ListarAtivosPorIdsAsync(servicosIds);
            if (servicos.Count != servicosIds.Distinct().Count())
                return ResultadoOperacao<OrdemServico>.DadosInvalidos("Um ou mais servicos nao foram encontrados ou estao inativos.");

            var pecasIds = pecasInput.Select(peca => peca.PecaInsumoId).Distinct().ToList();
            var pecas = await _pecas.ListarAtivasPorIdsAsync(pecasIds);
            if (pecas.Count != pecasIds.Count)
                return ResultadoOperacao<OrdemServico>.DadosInvalidos("Uma ou mais pecas/insumos nao foram encontrados ou estao inativos.");

            var ordem = new OrdemServico(cliente.Id, veiculo.Id, observacoes);

            foreach (var servico in servicos)
            {
                ordem.AdicionarServico(new OrdemServicoItem(
                    servico.Id,
                    servico.Nome,
                    servico.Preco,
                    servico.TempoEstimadoMinutos));
            }

            foreach (var item in pecasInput)
            {
                var peca = pecas.Single(entity => entity.Id == item.PecaInsumoId);
                if (peca.QuantidadeEstoque < item.Quantidade)
                    return ResultadoOperacao<OrdemServico>.DadosInvalidos($"Estoque insuficiente para {peca.Nome}.");

                ordem.AdicionarPeca(new OrdemServicoPeca(
                    peca.Id,
                    peca.Nome,
                    item.Quantidade,
                    peca.PrecoUnitario));
            }

            ordem.EnviarParaAprovacao();

            await _ordens.AdicionarAsync(ordem);
            await _ordens.SalvarAlteracoesAsync();

            var ordemCriada = await _ordens.ObterDetalhadaAsync(ordem.Id);
            return ResultadoOperacao<OrdemServico>.Ok(ordemCriada ?? ordem);
        }

        public async Task<ResultadoOperacao<OrdemServico>> AlterarStatusAsync(Guid id, StatusOrdemServico status)
        {
            var ordem = await _ordens.ObterDetalhadaAsync(id);
            if (ordem is null)
                return ResultadoOperacao<OrdemServico>.NaoEncontrado();

            try
            {
                switch (status)
                {
                    case StatusOrdemServico.EmDiagnostico:
                        ordem.IniciarDiagnostico();
                        break;
                    case StatusOrdemServico.AguardandoAprovacao:
                        ordem.EnviarParaAprovacao();
                        break;
                    case StatusOrdemServico.EmExecucao:
                        ordem.IniciarExecucao();
                        BaixarEstoque(ordem);
                        break;
                    case StatusOrdemServico.Finalizada:
                        ordem.Finalizar();
                        break;
                    case StatusOrdemServico.Entregue:
                        ordem.Entregar();
                        break;
                    default:
                        return ResultadoOperacao<OrdemServico>.DadosInvalidos("Status informado nao pode ser aplicado manualmente.");
                }

                await _ordens.SalvarAlteracoesAsync();
                return ResultadoOperacao<OrdemServico>.Ok(ordem);
            }
            catch (InvalidOperationException ex)
            {
                return ResultadoOperacao<OrdemServico>.DadosInvalidos(ex.Message);
            }
        }

        public async Task<ResultadoOperacao<OrdemServico>> AprovarAsync(Guid id, string cpfCnpj)
        {
            var ordem = await _ordens.ObterDetalhadaAsync(id);
            if (ordem is null)
                return ResultadoOperacao<OrdemServico>.NaoEncontrado();

            if (!DocumentoValidator.IsValid(cpfCnpj) || ordem.Cliente?.CpfCnpj != DocumentoValidator.Normalize(cpfCnpj))
                return ResultadoOperacao<OrdemServico>.NaoAutorizado("Documento nao confere com a ordem de servico.");

            try
            {
                ordem.Aprovar();
                BaixarEstoque(ordem);
                await _ordens.SalvarAlteracoesAsync();
                return ResultadoOperacao<OrdemServico>.Ok(ordem);
            }
            catch (InvalidOperationException ex)
            {
                return ResultadoOperacao<OrdemServico>.DadosInvalidos(ex.Message);
            }
        }

        public async Task<ResultadoOperacao<OrdemServico>> ConsultarClienteAsync(Guid id, string cpfCnpj)
        {
            var ordem = await _ordens.ObterDetalhadaAsync(id);
            if (ordem is null)
                return ResultadoOperacao<OrdemServico>.NaoEncontrado();

            if (!DocumentoValidator.IsValid(cpfCnpj) || ordem.Cliente?.CpfCnpj != DocumentoValidator.Normalize(cpfCnpj))
                return ResultadoOperacao<OrdemServico>.NaoAutorizado("Documento nao confere com a ordem de servico.");

            return ResultadoOperacao<OrdemServico>.Ok(ordem);
        }

        public async Task<TempoMedioExecucao> CalcularTempoMedioExecucaoAsync()
        {
            return await _ordens.CalcularTempoMedioExecucaoAsync();
        }

        private async Task<Veiculo?> ObterOuCriarVeiculoAsync(VeiculoOrdemInput input, Guid clienteId)
        {
            if (!PlacaValidator.IsValid(input.Placa))
                return null;

            var placa = PlacaValidator.Normalize(input.Placa);
            var veiculo = await _veiculos.ObterPorPlacaAsync(placa);
            if (veiculo is not null)
                return veiculo.ClienteId == clienteId ? veiculo : null;

            veiculo = new Veiculo(clienteId, placa, input.Marca, input.Modelo, input.Ano);
            await _veiculos.AdicionarAsync(veiculo);
            await _veiculos.SalvarAlteracoesAsync();

            return veiculo;
        }

        private static void BaixarEstoque(OrdemServico ordem)
        {
            foreach (var item in ordem.Pecas)
                item.PecaInsumo?.BaixarEstoque(item.Quantidade);
        }
    }
}
