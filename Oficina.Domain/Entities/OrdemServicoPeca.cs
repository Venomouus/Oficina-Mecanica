using System.Diagnostics.CodeAnalysis;

namespace Oficina.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class OrdemServicoPeca
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OrdemServicoId { get; set; }
        public OrdemServico? OrdemServico { get; set; }
        public Guid PecaInsumoId { get; set; }
        public PecaInsumo? PecaInsumo { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public decimal ValorUnitario { get; set; }
    }
}
