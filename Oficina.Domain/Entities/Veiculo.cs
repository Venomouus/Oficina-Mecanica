namespace Oficina.Domain.Entities
{
    public class Veiculo
    {
        public Guid Id { get; private set; } = Guid.NewGuid();

        public string Placa { get; private set; } = string.Empty;
        public string Marca { get; private set; } = string.Empty;
        public string Modelo { get; private set; } = string.Empty;
        public int Ano { get; private set; }

        public Guid ClienteId { get; private set; }
        public Cliente? Cliente { get; private set; }

        protected Veiculo()
        {
        }

        public Veiculo(Guid clienteId, string placa, string marca, string modelo, int ano)
        {
            ClienteId = clienteId;
            Placa = placa;
            Marca = marca;
            Modelo = modelo;
            Ano = ano;
        }

        public void Atualizar(string placa, string marca, string modelo, int ano)
        {
            Placa = placa;
            Marca = marca;
            Modelo = modelo;
            Ano = ano;
        }
    }
}