namespace Oficina.Domain.Entities
{
    public class OrdemServicoPeca
    {
        public Guid Id { get; private set; } = Guid.NewGuid();

        public Guid OrdemServicoId { get; private set; }
        public OrdemServico? OrdemServico { get; private set; }

        public Guid PecaInsumoId { get; private set; }
        public PecaInsumo? PecaInsumo { get; private set; }

        public string Nome { get; private set; } = string.Empty;
        public int Quantidade { get; private set; }
        public decimal ValorUnitario { get; private set; }

        public decimal ValorTotal => Quantidade * ValorUnitario;

        protected OrdemServicoPeca()
        {
        }

        public OrdemServicoPeca(Guid pecaInsumoId, string nome, int quantidade, decimal valorUnitario)
        {
            PecaInsumoId = pecaInsumoId;
            Nome = nome;
            Quantidade = quantidade;
            ValorUnitario = valorUnitario;
        }
    }
}