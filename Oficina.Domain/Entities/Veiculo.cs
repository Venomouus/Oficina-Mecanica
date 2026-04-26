using System.Diagnostics.CodeAnalysis;

namespace Oficina.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class Veiculo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Placa { get; set; } = string.Empty;
        public string Marca { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public int Ano { get; set; }
        public Guid ClienteId { get; set; }
        public Cliente? Cliente { get; set; }
    }
}
