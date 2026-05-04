namespace Oficina.Domain.Entities
{
    public class Servico
    {
        public Guid Id { get; private set; } = Guid.NewGuid();

        public string Nome { get; private set; } = string.Empty;
        public string Descricao { get; private set; } = string.Empty;
        public decimal Preco { get; private set; }
        public int TempoEstimadoMinutos { get; private set; }
        public bool Ativo { get; private set; } = true;

        protected Servico()
        {
        }

        public Servico(string nome, string descricao, decimal preco, int tempoEstimadoMinutos)
        {
            Nome = nome;
            Descricao = descricao;
            Preco = preco;
            TempoEstimadoMinutos = tempoEstimadoMinutos;
        }

        public void Atualizar(string nome, string descricao, decimal preco, int tempoEstimadoMinutos)
        {
            Nome = nome;
            Descricao = descricao;
            Preco = preco;
            TempoEstimadoMinutos = tempoEstimadoMinutos;
        }

        public void Desativar()
        {
            Ativo = false;
        }

        public void Ativar()
        {
            Ativo = true;
        }
    }
}