using System.Diagnostics.CodeAnalysis;

namespace Oficina.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class Servico
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Nome { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public decimal Preco { get; set; }
        public int TempoEstimadoMinutos { get; set; }
        public bool Ativo { get; set; } = true;
    }
}
