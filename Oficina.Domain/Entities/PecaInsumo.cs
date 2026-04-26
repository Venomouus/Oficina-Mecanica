namespace Oficina.Domain.Entities
{
    public class PecaInsumo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Nome { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public decimal PrecoUnitario { get; set; }
        public int QuantidadeEstoque { get; set; }
        public int EstoqueMinimo { get; set; }
        public bool Ativo { get; set; } = true;

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
    }
}
