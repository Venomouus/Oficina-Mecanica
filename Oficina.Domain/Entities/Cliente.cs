using System.Diagnostics.CodeAnalysis;

namespace Oficina.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class Cliente
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Nome { get; set; } = string.Empty;
        public string CpfCnpj { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<Veiculo> Veiculos { get; set; } = new();
    }
}
