using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oficina.API.Contracts;
using Oficina.Domain.Entities;
using Oficina.Domain.Validation;
using Oficina.Infrastructure.Persistence;

namespace Oficina.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ClientesController : ControllerBase
{
    private readonly OficinaDbContext _context;

    public ClientesController(OficinaDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _context.Clientes.AsNoTracking().ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var cliente = await _context.Clientes.AsNoTracking().Include(item => item.Veiculos).FirstOrDefaultAsync(item => item.Id == id);
        return cliente is null ? NotFound() : Ok(cliente);
    }

    [HttpPost]
    public async Task<IActionResult> Post(ClienteRequest request)
    {
        if (!DocumentoValidator.IsValid(request.CpfCnpj))
            return BadRequest(new { message = "CPF/CNPJ invalido." });

        var documento = DocumentoValidator.Normalize(request.CpfCnpj);
        if (await _context.Clientes.AnyAsync(item => item.CpfCnpj == documento))
            return Conflict(new { message = "Cliente ja cadastrado com este CPF/CNPJ." });

        var cliente = new Cliente
        {
            Nome = request.Nome,
            CpfCnpj = documento,
            Telefone = request.Telefone,
            Email = request.Email
        };

        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = cliente.Id }, cliente);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Put(Guid id, ClienteRequest request)
    {
        if (!DocumentoValidator.IsValid(request.CpfCnpj))
            return BadRequest(new { message = "CPF/CNPJ invalido." });

        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente is null)
            return NotFound();

        var documento = DocumentoValidator.Normalize(request.CpfCnpj);
        if (await _context.Clientes.AnyAsync(item => item.Id != id && item.CpfCnpj == documento))
            return Conflict(new { message = "Outro cliente ja usa este CPF/CNPJ." });

        cliente.Nome = request.Nome;
        cliente.CpfCnpj = documento;
        cliente.Telefone = request.Telefone;
        cliente.Email = request.Email;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente is null)
            return NotFound();

        _context.Clientes.Remove(cliente);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
