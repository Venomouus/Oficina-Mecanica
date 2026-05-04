using System.Diagnostics.CodeAnalysis;

namespace Oficina.Domain.Entities
{
    public class Cliente
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public string Nome { get; private set; } = string.Empty;
        public string CpfCnpj { get; private set; } = string.Empty;
        public string Telefone { get; private set; } = string.Empty;
        public string Email { get; private set; } = string.Empty;

        public List<Veiculo> Veiculos { get; private set; } = [];

        protected Cliente() { }

        public Cliente(string nome, string cpfCnpj, string telefone, string email)
        {
            Nome = nome;
            CpfCnpj = cpfCnpj;
            Telefone = telefone;
            Email = email;
        }

        public void Atualizar(string nome, string telefone, string email)
        {
            Nome = nome;
            Telefone = telefone;
            Email = email;
        }
    }
}
