using Oficina.Domain.Enums;

namespace Oficina.Domain.Entities
{
    public class OrdemServico
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public string Numero { get; private set; } = $"OS-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        public Guid ClienteId { get; private set; }
        public Cliente? Cliente { get; private set; }

        public Guid VeiculoId { get; private set; }
        public Veiculo? Veiculo { get; private set; }

        public StatusOrdemServico Status { get; private set; } = StatusOrdemServico.Recebida;
        public decimal ValorTotal { get; private set; }

        public DateTime CriadaEm { get; private set; } = DateTime.UtcNow;
        public DateTime? AprovadaEm { get; private set; }
        public DateTime? IniciadaEm { get; private set; }
        public DateTime? FinalizadaEm { get; private set; }
        public DateTime? EntregueEm { get; private set; }

        public string? Observacoes { get; private set; }

        public List<OrdemServicoItem> Servicos { get; private set; } = new();
        public List<OrdemServicoPeca> Pecas { get; private set; } = new();

        protected OrdemServico()
        {
        }

        public OrdemServico(Guid clienteId, Guid veiculoId, string? observacoes = null)
        {
            ClienteId = clienteId;
            VeiculoId = veiculoId;
            Observacoes = observacoes;
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

            Status = StatusOrdemServico.EmDiagnostico;
        }

        public void EnviarParaAprovacao()
        {
            CalcularOrcamento();

            Status = StatusOrdemServico.AguardandoAprovacao;
        }

        public void Aprovar()
        {
            GarantirStatus(StatusOrdemServico.AguardandoAprovacao);

            AprovadaEm = DateTime.UtcNow;
            IniciadaEm = DateTime.UtcNow;
            Status = StatusOrdemServico.EmExecucao;
        }

        public void IniciarExecucao()
        {
            if (Status != StatusOrdemServico.EmDiagnostico && Status != StatusOrdemServico.AguardandoAprovacao)
                throw new InvalidOperationException("A OS precisa estar em diagnostico ou aguardando aprovacao para iniciar a execucao.");

            if (Status == StatusOrdemServico.AguardandoAprovacao)
                AprovadaEm ??= DateTime.UtcNow;

            IniciadaEm = DateTime.UtcNow;
            Status = StatusOrdemServico.EmExecucao;
        }

        public void Finalizar()
        {
            GarantirStatus(StatusOrdemServico.EmExecucao);

            FinalizadaEm = DateTime.UtcNow;
            Status = StatusOrdemServico.Finalizada;
        }

        public void Entregar()
        {
            GarantirStatus(StatusOrdemServico.Finalizada);

            EntregueEm = DateTime.UtcNow;
            Status = StatusOrdemServico.Entregue;
        }

        private void GarantirStatus(StatusOrdemServico statusEsperado)
        {
            if (Status != statusEsperado)
                throw new InvalidOperationException($"A OS precisa estar com status {statusEsperado} para realizar esta acao.");
        }
    }
}
