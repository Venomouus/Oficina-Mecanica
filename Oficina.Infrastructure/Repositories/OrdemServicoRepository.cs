using Microsoft.EntityFrameworkCore;
using Oficina.Application.Interfaces;
using Oficina.Application.Models;
using Oficina.Domain.Entities;
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

        public async Task<OrdemServico?> ObterDetalhadaAsync(Guid id)
        {
            return await _context.OrdensServico
                .Include(ordem => ordem.Cliente)
                .Include(ordem => ordem.Veiculo)
                .Include(ordem => ordem.Servicos)
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
            await _context.SaveChangesAsync();
        }
    }
}
