using Microsoft.EntityFrameworkCore;
using Oficina.Application.Interfaces;
using Oficina.Domain.Entities;
using Oficina.Infrastructure.Persistence;

namespace Oficina.Infrastructure.Repositories
{
    public class PecaInsumoRepository : IPecaInsumoRepository
    {
        private readonly OficinaDbContext _context;

        public PecaInsumoRepository(OficinaDbContext context)
        {
            _context = context;
        }

        public async Task<List<PecaInsumo>> ListarAsync()
        {
            return await _context.PecasInsumos.AsNoTracking().ToListAsync();
        }

        public async Task<PecaInsumo?> ObterPorIdAsync(Guid id)
        {
            return await _context.PecasInsumos.FindAsync(id);
        }

        public async Task<List<PecaInsumo>> ListarAtivasPorIdsAsync(IEnumerable<Guid> ids)
        {
            var idsBusca = ids.ToList();
            return await _context.PecasInsumos
                .Where(peca => idsBusca.Contains(peca.Id) && peca.Ativo)
                .ToListAsync();
        }

        public async Task<bool> CodigoExisteAsync(string codigo, Guid? ignorarId = null)
        {
            return await _context.PecasInsumos.AnyAsync(peca =>
                peca.Codigo == codigo && (!ignorarId.HasValue || peca.Id != ignorarId.Value));
        }

        public async Task AdicionarAsync(PecaInsumo peca)
        {
            await _context.PecasInsumos.AddAsync(peca);
        }

        public void Remover(PecaInsumo peca)
        {
            _context.PecasInsumos.Remove(peca);
        }

        public async Task SalvarAlteracoesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
