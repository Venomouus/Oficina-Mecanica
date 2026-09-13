using Oficina.Domain.Enums;

namespace Oficina.Domain.Entities;

public sealed class HistoricoStatusOrdemServico
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrdemServicoId { get; private set; }
    public int Sequencia { get; private set; }
    public StatusOrdemServico Status { get; private set; }
    // Null indica que a entrada foi importada sem conhecer o inicio real do status.
    public DateTime? IniciadaEm { get; private set; }
    public DateTime? FinalizadaEm { get; private set; }
    public DateTime RegistradaEm { get; private set; }

    private HistoricoStatusOrdemServico() { }

    internal HistoricoStatusOrdemServico(Guid ordemServicoId, int sequencia, StatusOrdemServico status, DateTime instante)
    {
        OrdemServicoId = ordemServicoId;
        Sequencia = sequencia;
        Status = status;
        IniciadaEm = instante;
        RegistradaEm = instante;
    }

    internal void Encerrar(DateTime instante)
    {
        if (FinalizadaEm.HasValue || instante < (IniciadaEm ?? RegistradaEm))
            throw new InvalidOperationException("O periodo de status nao pode ser encerrado nesse instante.");

        FinalizadaEm = instante;
    }
}
