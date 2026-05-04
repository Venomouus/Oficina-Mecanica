using Microsoft.EntityFrameworkCore;
using Oficina.Application.Interfaces;
using Oficina.Domain.Entities;
using Oficina.Infrastructure.Persistence;

namespace Oficina.Infrastructure.Repositories
{
    public class VeiculoRepository : IVeiculoRepository
    {
        private readonly OficinaDbContext _context;

        public VeiculoRepository(OficinaDbContext context)
        {
            _context = context;
        }

        public async Task<List<Veiculo>> ListarAsync()
        {
            return await _context.Veiculos.ToListAsync();
        }

        public async Task<Veiculo?> ObterPorIdAsync(Guid id)
        {
            return await _context.Veiculos.FindAsync(id);
        }

        public async Task<Veiculo?> ObterPorPlacaAsync(string placa)
        {
            return await _context.Veiculos.FirstOrDefaultAsync(veiculo => veiculo.Placa == placa);
        }

        public async Task<bool> PossuiOrdensServicoAsync(Guid id)
        {
            return await _context.OrdensServico.AnyAsync(ordem => ordem.VeiculoId == id);
        }

        public async Task AdicionarAsync(Veiculo veiculo)
        {
            await _context.Veiculos.AddAsync(veiculo);
        }

        public void Remover(Veiculo veiculo)
        {
            _context.Veiculos.Remove(veiculo);
        }

        public async Task SalvarAlteracoesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
