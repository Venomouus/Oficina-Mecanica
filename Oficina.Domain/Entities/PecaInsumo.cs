namespace Oficina.Domain.Entities
{
    public class PecaInsumo
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public string Nome { get; private set; } = string.Empty;
        public string Codigo { get; private set; } = string.Empty;
        public decimal PrecoUnitario { get; private set; }
        public int QuantidadeEstoque { get; private set; }
        public int EstoqueMinimo { get; private set; }
        public bool Ativo { get; private set; } = true;

        protected PecaInsumo()
        {
        }

        public PecaInsumo(string nome, string codigo, decimal precoUnitario, int quantidadeEstoque, int estoqueMinimo)
        {
            Nome = nome;
            Codigo = codigo;
            PrecoUnitario = precoUnitario;
            QuantidadeEstoque = quantidadeEstoque;
            EstoqueMinimo = estoqueMinimo;
        }

        public void Atualizar(string nome, string codigo, decimal precoUnitario, int estoqueMinimo)
        {
            Nome = nome;
            Codigo = codigo;
            PrecoUnitario = precoUnitario;
            EstoqueMinimo = estoqueMinimo;
        }

        public void BaixarEstoque(int quantidade)
        {
            if (quantidade <= 0)
                throw new InvalidOperationException("Quantidade deve ser maior que zero.");

            if (QuantidadeEstoque < quantidade)
                throw new InvalidOperationException($"Estoque insuficiente para {Nome}.");

            QuantidadeEstoque -= quantidade;
        }

        public void ReporEstoque(int quantidade)
        {
            if (quantidade <= 0)
                throw new InvalidOperationException("Quantidade deve ser maior que zero.");

            QuantidadeEstoque += quantidade;
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