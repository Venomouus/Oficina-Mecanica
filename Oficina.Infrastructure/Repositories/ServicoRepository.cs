using Microsoft.EntityFrameworkCore;
using Oficina.Application.Interfaces;
using Oficina.Domain.Entities;
using Oficina.Infrastructure.Persistence;

namespace Oficina.Infrastructure.Repositories
{
    public class ServicoRepository : IServicoRepository
    {
        private readonly OficinaDbContext _context;

        public ServicoRepository(OficinaDbContext context)
        {
            _context = context;
        }

        public async Task<List<Servico>> ListarAsync()
        {
            return await _context.Servicos.ToListAsync();
        }

        public async Task<Servico?> ObterPorIdAsync(Guid id)
        {
            return await _context.Servicos.FindAsync(id);
        }

        public async Task<List<Servico>> ListarAtivosPorIdsAsync(IEnumerable<Guid> ids)
        {
            var idsBusca = ids.ToList();
            return await _context.Servicos
                .Where(servico => idsBusca.Contains(servico.Id) && servico.Ativo)
                .ToListAsync();
        }

        public async Task AdicionarAsync(Servico servico)
        {
            await _context.Servicos.AddAsync(servico);
        }

        public void Remover(Servico servico)
        {
            _context.Servicos.Remove(servico);
        }

        public async Task SalvarAlteracoesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
