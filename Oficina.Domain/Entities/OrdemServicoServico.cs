using System.Diagnostics.CodeAnalysis;

namespace Oficina.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class OrdemServicoServico
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OrdemServicoId { get; set; }
        public OrdemServico? OrdemServico { get; set; }
        public Guid ServicoId { get; set; }
        public Servico? Servico { get; set; }
        public string Nome { get; set; } = string.Empty;
        public decimal ValorUnitario { get; set; }
        public int TempoEstimadoMinutos { get; set; }
    }
}
