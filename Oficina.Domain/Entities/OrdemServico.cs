using Oficina.Domain.Enums;

namespace Oficina.Domain.Entities
{
    public class OrdemServico
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Numero { get; set; } = $"OS-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        public Guid ClienteId { get; set; }
        public Cliente? Cliente { get; set; }
        public Guid VeiculoId { get; set; }
        public Veiculo? Veiculo { get; set; }
        public StatusOrdemServico Status { get; private set; } = StatusOrdemServico.Recebida;
        public decimal ValorTotal { get; private set; }
        public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
        public DateTime? AprovadaEm { get; private set; }
        public DateTime? IniciadaEm { get; private set; }
        public DateTime? FinalizadaEm { get; private set; }
        public DateTime? EntregueEm { get; private set; }
        public string? Observacoes { get; set; }
        public List<OrdemServicoServico> Servicos { get; set; } = new();
        public List<OrdemServicoPeca> Pecas { get; set; } = new();

        public void CalcularOrcamento()
        {
            ValorTotal = Servicos.Sum(item => item.ValorUnitario)
                + Pecas.Sum(item => item.ValorUnitario * item.Quantidade);
        }

        public void EnviarParaAprovacao()
        {
            CalcularOrcamento();
            Status = StatusOrdemServico.AguardandoAprovacao;
        }

        public void IniciarDiagnostico()
        {
            GarantirStatus(StatusOrdemServico.Recebida);
            Status = StatusOrdemServico.EmDiagnostico;
        }

        public void Aprovar()
        {
            GarantirStatus(StatusOrdemServico.AguardandoAprovacao);
            AprovadaEm = DateTime.UtcNow;
            Status = StatusOrdemServico.EmExecucao;
            IniciadaEm = DateTime.UtcNow;
        }

        public void IniciarExecucao()
        {
            if (Status != StatusOrdemServico.AguardandoAprovacao && Status != StatusOrdemServico.EmDiagnostico)
                throw new InvalidOperationException("A OS precisa estar em diagnostico ou aguardando aprovacao.");

            Status = StatusOrdemServico.EmExecucao;
            IniciadaEm ??= DateTime.UtcNow;
        }

        public void Finalizar()
        {
            GarantirStatus(StatusOrdemServico.EmExecucao);
            Status = StatusOrdemServico.Finalizada;
            FinalizadaEm = DateTime.UtcNow;
        }

        public void Entregar()
        {
            GarantirStatus(StatusOrdemServico.Finalizada);
            Status = StatusOrdemServico.Entregue;
            EntregueEm = DateTime.UtcNow;
        }

        private void GarantirStatus(StatusOrdemServico esperado)
        {
            if (Status != esperado)
                throw new InvalidOperationException($"Status atual da OS nao permite esta acao. Status atual: {Status}.");
        }
    }
}
