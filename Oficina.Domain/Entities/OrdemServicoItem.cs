namespace Oficina.Domain.Entities
{
    public class OrdemServicoItem
    {
        public Guid Id { get; private set; } = Guid.NewGuid();

        public Guid OrdemServicoId { get; private set; }
        public OrdemServico? OrdemServico { get; private set; }

        public Guid ServicoId { get; private set; }
        public Servico? Servico { get; private set; }

        public string Nome { get; private set; } = string.Empty;
        public decimal ValorUnitario { get; private set; }
        public int TempoEstimadoMinutos { get; private set; }

        protected OrdemServicoItem()
        {
        }

        public OrdemServicoItem(Guid servicoId, string nome, decimal valorUnitario, int tempoEstimadoMinutos)
        {
            ServicoId = servicoId;
            Nome = nome;
            ValorUnitario = valorUnitario;
            TempoEstimadoMinutos = tempoEstimadoMinutos;
        }
    }
}