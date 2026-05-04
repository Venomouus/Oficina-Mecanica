using Microsoft.EntityFrameworkCore;
using Oficina.Application.Interfaces;
using Oficina.Domain.Entities;
using Oficina.Domain.Validation;
using Oficina.Infrastructure.Persistence;

namespace Oficina.Infrastructure.Repositories
{
    public class ClienteRepository : IClienteRepository
    {
        private readonly OficinaDbContext _context;

        public ClienteRepository(OficinaDbContext context)
        {
            _context = context;
        }

        public async Task<List<Cliente>> ListarAsync()
        {
            return await _context.Clientes
                .Include(cliente => cliente.Veiculos)
                .ToListAsync();
        }

        public async Task<Cliente?> ObterPorIdAsync(Guid id)
        {
            return await _context.Clientes
                .Include(cliente => cliente.Veiculos)
                .FirstOrDefaultAsync(cliente => cliente.Id == id);
        }

        public async Task<Cliente?> ObterPorDocumentoAsync(string cpfCnpj)
        {
            var documento = DocumentoValidator.Normalize(cpfCnpj);
            var documentoFormatado = FormatarDocumento(documento);

            return await _context.Clientes.FirstOrDefaultAsync(cliente =>
                cliente.CpfCnpj == documento ||
                cliente.CpfCnpj == documentoFormatado);
        }

        public async Task<bool> ExisteAsync(Guid id)
        {
            return await _context.Clientes.AnyAsync(cliente => cliente.Id == id);
        }

        public async Task<bool> PossuiVinculosAsync(Guid id)
        {
            return await _context.Veiculos.AnyAsync(veiculo => veiculo.ClienteId == id)
                || await _context.OrdensServico.AnyAsync(ordem => ordem.ClienteId == id);
        }

        public async Task AdicionarAsync(Cliente cliente)
        {
            await _context.Clientes.AddAsync(cliente);
        }

        public void Remover(Cliente cliente)
        {
            _context.Clientes.Remove(cliente);
        }

        public async Task SalvarAlteracoesAsync()
        {
            await _context.SaveChangesAsync();
        }

        private static string FormatarDocumento(string documento)
        {
            return documento.Length switch
            {
                11 => $"{documento[..3]}.{documento.Substring(3, 3)}.{documento.Substring(6, 3)}-{documento[9..]}",
                14 => $"{documento[..2]}.{documento.Substring(2, 3)}.{documento.Substring(5, 3)}/{documento.Substring(8, 4)}-{documento[12..]}",
                _ => documento
            };
        }
    }
}
