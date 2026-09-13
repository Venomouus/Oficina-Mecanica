using Oficina.Domain.Enums;

namespace Oficina.Domain.Entities
{
    public class OrdemServico
    {
        private readonly TimeProvider _timeProvider = TimeProvider.System;
        private readonly List<HistoricoStatusOrdemServico> _historicoStatus = new();

        public Guid Id { get; private set; } = Guid.NewGuid();
        public string Numero { get; private set; } = $"OS-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        public Guid ClienteId { get; private set; }
        public Cliente? Cliente { get; private set; }

        public Guid VeiculoId { get; private set; }
        public Veiculo? Veiculo { get; private set; }

        public StatusOrdemServico Status { get; private set; } = StatusOrdemServico.Recebida;
        public int Versao { get; private set; }
        public IReadOnlyCollection<HistoricoStatusOrdemServico> HistoricoStatus => _historicoStatus.AsReadOnly();
        public decimal ValorTotal { get; private set; }

        public DateTime CriadaEm { get; private set; } = DateTime.UtcNow;
        public DateTime? AprovadaEm { get; private set; }
        public DateTime? IniciadaEm { get; private set; }
        public DateTime? FinalizadaEm { get; private set; }
        public DateTime? EntregueEm { get; private set; }
        public bool? OrcamentoAprovado { get; private set; }
        public DateTime? OrcamentoRespondidoEm { get; private set; }
        public string? MotivoRecusaOrcamento { get; private set; }

        public string? Observacoes { get; private set; }

        public List<OrdemServicoItem> Servicos { get; private set; } = new();
        public List<OrdemServicoPeca> Pecas { get; private set; } = new();

        protected OrdemServico()
        {
        }

        public OrdemServico(Guid clienteId, Guid veiculoId, string? observacoes = null, TimeProvider? timeProvider = null)
        {
            _timeProvider = timeProvider ?? TimeProvider.System;
            ClienteId = clienteId;
            VeiculoId = veiculoId;
            Observacoes = observacoes;
            CriadaEm = _timeProvider.GetUtcNow().UtcDateTime;
            _historicoStatus.Add(new HistoricoStatusOrdemServico(Id, 1, Status, CriadaEm));
        }

        public void AtualizarObservacoes(string? observacoes)
        {
            Observacoes = observacoes;
        }

        public void AdicionarServico(OrdemServicoItem servico)
        {
            Servicos.Add(servico);
            CalcularOrcamento();
        }

        public void AdicionarPeca(OrdemServicoPeca peca)
        {
            Pecas.Add(peca);
            CalcularOrcamento();
        }

        public void CalcularOrcamento()
        {
            var totalServicos = Servicos.Sum(item => item.ValorUnitario);
            var totalPecas = Pecas.Sum(item => item.ValorUnitario * item.Quantidade);

            ValorTotal = totalServicos + totalPecas;
        }

        public void IniciarDiagnostico()
        {
            GarantirStatus(StatusOrdemServico.Recebida);

            MudarStatus(StatusOrdemServico.EmDiagnostico);
        }

        public void EnviarParaAprovacao()
        {
            if (Status != StatusOrdemServico.Recebida && Status != StatusOrdemServico.EmDiagnostico && Status != StatusOrdemServico.AguardandoAprovacao)
                throw new InvalidOperationException("A OS precisa estar recebida, em diagnostico ou aguardando aprovacao para enviar o orcamento.");

            if (Status != StatusOrdemServico.AguardandoAprovacao)
                MudarStatus(StatusOrdemServico.AguardandoAprovacao);
            else
                Versao++; // Reenvio do orcamento sem criar um periodo de status duplicado.

            CalcularOrcamento();

            OrcamentoAprovado = null;
            OrcamentoRespondidoEm = null;
            MotivoRecusaOrcamento = null;
        }

        public void Aprovar()
        {
            GarantirStatus(StatusOrdemServico.AguardandoAprovacao);
            var instante = MudarStatus(StatusOrdemServico.EmExecucao);

            OrcamentoAprovado = true;
            OrcamentoRespondidoEm = instante;
            MotivoRecusaOrcamento = null;
            AprovadaEm = instante;
            IniciadaEm = instante;
        }

        public void RecusarOrcamento(string? motivo)
        {
            GarantirStatus(StatusOrdemServico.AguardandoAprovacao);

            OrcamentoAprovado = false;
            OrcamentoRespondidoEm = _timeProvider.GetUtcNow().UtcDateTime;
            MotivoRecusaOrcamento = string.IsNullOrWhiteSpace(motivo)
                ? null
                : motivo.Trim();
            Versao++;
        }

        public void IniciarExecucao()
        {
            if (Status != StatusOrdemServico.EmDiagnostico && Status != StatusOrdemServico.AguardandoAprovacao)
                throw new InvalidOperationException("A OS precisa estar em diagnostico ou aguardando aprovacao para iniciar a execucao.");

            var aguardavaAprovacao = Status == StatusOrdemServico.AguardandoAprovacao;
            if (aguardavaAprovacao && OrcamentoAprovado == false)
                throw new InvalidOperationException("O orcamento desta OS foi recusado pelo cliente.");

            var instante = MudarStatus(StatusOrdemServico.EmExecucao);
            if (aguardavaAprovacao)
            {
                OrcamentoAprovado ??= true;
                OrcamentoRespondidoEm ??= instante;
                AprovadaEm ??= instante;
            }

            IniciadaEm = instante;
        }

        public void Finalizar()
        {
            GarantirStatus(StatusOrdemServico.EmExecucao);

            FinalizadaEm = MudarStatus(StatusOrdemServico.Finalizada);
        }

        public void Entregar()
        {
            GarantirStatus(StatusOrdemServico.Finalizada);

            EntregueEm = MudarStatus(StatusOrdemServico.Entregue);
        }

        private DateTime MudarStatus(StatusOrdemServico novoStatus)
        {
            var atual = _historicoStatus.SingleOrDefault(periodo => !periodo.FinalizadaEm.HasValue);
            if (atual is null || atual.Status != Status)
                throw new InvalidOperationException("Carregue o historico completo antes de alterar o status da OS.");

            var instante = _timeProvider.GetUtcNow().UtcDateTime;
            atual.Encerrar(instante);
            _historicoStatus.Add(new HistoricoStatusOrdemServico(Id, atual.Sequencia + 1, novoStatus, instante));
            Status = novoStatus;
            Versao++;
            return instante;
        }

        private void GarantirStatus(StatusOrdemServico statusEsperado)
        {
            if (Status != statusEsperado)
                throw new InvalidOperationException($"A OS precisa estar com status {statusEsperado} para realizar esta acao.");
        }
    }
}
