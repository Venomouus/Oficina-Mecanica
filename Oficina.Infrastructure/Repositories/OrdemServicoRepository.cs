using Microsoft.EntityFrameworkCore;
using Npgsql;
using Oficina.Application.Common;
using Oficina.Application.Interfaces;
using Oficina.Application.Models;
using Oficina.Domain.Entities;
using Oficina.Domain.Enums;
using Oficina.Infrastructure.Persistence;

namespace Oficina.Infrastructure.Repositories
{
    public class OrdemServicoRepository : IOrdemServicoRepository
    {
        private readonly OficinaDbContext _context;

        public OrdemServicoRepository(OficinaDbContext context)
        {
            _context = context;
        }

        public async Task<List<OrdemServicoResumo>> ListarResumoAsync()
        {
            return await _context.OrdensServico
                .AsNoTracking()
                .OrderByDescending(ordem => ordem.CriadaEm)
                .Select(ordem => new OrdemServicoResumo(
                    ordem.Id,
                    ordem.Numero,
                    ordem.Status,
                    ordem.ValorTotal,
                    ordem.CriadaEm))
                .ToListAsync();
        }

        public async Task<List<OrdemServicoResumo>> ListarFilaOperacionalAsync()
        {
            return await _context.OrdensServico
                .AsNoTracking()
                .Where(ordem =>
                    ordem.Status != StatusOrdemServico.Finalizada &&
                    ordem.Status != StatusOrdemServico.Entregue)
                .Select(ordem => new
                {
                    ordem.Id,
                    ordem.Numero,
                    ordem.Status,
                    ordem.ValorTotal,
                    ordem.CriadaEm,
                    Prioridade = ordem.Status == StatusOrdemServico.EmExecucao ? 1
                        : ordem.Status == StatusOrdemServico.AguardandoAprovacao ? 2
                        : ordem.Status == StatusOrdemServico.EmDiagnostico ? 3
                        : ordem.Status == StatusOrdemServico.Recebida ? 4
                        : 99
                })
                .OrderBy(ordem => ordem.Prioridade)
                .ThenBy(ordem => ordem.CriadaEm)
                .Select(ordem => new OrdemServicoResumo(
                    ordem.Id,
                    ordem.Numero,
                    ordem.Status,
                    ordem.ValorTotal,
                    ordem.CriadaEm))
                .ToListAsync();
        }

        public async Task<List<OrdemServicoResumo>> ListarPorClienteAsync(Guid clienteId)
        {
            return await _context.OrdensServico.AsNoTracking()
                .Where(ordem => ordem.ClienteId == clienteId)
                .OrderByDescending(ordem => ordem.CriadaEm)
                .Select(ordem => new OrdemServicoResumo(ordem.Id, ordem.Numero, ordem.Status, ordem.ValorTotal, ordem.CriadaEm))
                .ToListAsync();
        }

        public async Task<OrdemServico?> ObterDetalhadaAsync(Guid id)
        {
            return await _context.OrdensServico
                .Include(ordem => ordem.Cliente)
                .Include(ordem => ordem.Veiculo)
                .Include(ordem => ordem.Servicos)
                .Include(ordem => ordem.HistoricoStatus)
                .Include(ordem => ordem.Pecas)
                .ThenInclude(peca => peca.PecaInsumo)
                .FirstOrDefaultAsync(ordem => ordem.Id == id);
        }

        public async Task AdicionarAsync(OrdemServico ordem)
        {
            await _context.OrdensServico.AddAsync(ordem);
        }

        public async Task<TempoMedioExecucao> CalcularTempoMedioExecucaoAsync()
        {
            var finalizadas = await _context.OrdensServico
                .AsNoTracking()
                .Where(ordem => ordem.IniciadaEm != null && ordem.FinalizadaEm != null)
                .Select(ordem => new { ordem.IniciadaEm, ordem.FinalizadaEm })
                .ToListAsync();

            var mediaMinutos = finalizadas.Count == 0
                ? 0
                : finalizadas.Average(ordem => (ordem.FinalizadaEm!.Value - ordem.IniciadaEm!.Value).TotalMinutes);

            return new TempoMedioExecucao(finalizadas.Count, Math.Round(mediaMinutos, 2));
        }

        public async Task SalvarAlteracoesAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new ConflitoConcorrenciaException("A OS foi alterada por outra requisicao. Consulte novamente antes de repetir a operacao.", ex);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_HistoricoStatusOrdemServico_OrdemServicoId_Sequencia" })
            {
                throw new ConflitoConcorrenciaException("O historico da OS foi alterado por outra requisicao. Consulte novamente antes de repetir a operacao.", ex);
            }
        }
    }
}
