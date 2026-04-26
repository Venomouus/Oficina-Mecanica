using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oficina.API.Contracts;
using Oficina.Domain.Entities;
using Oficina.Infrastructure.Persistence;

namespace Oficina.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ServicosController : ControllerBase
{
    private readonly OficinaDbContext _context;

    public ServicosController(OficinaDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _context.Servicos.AsNoTracking().ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var servico = await _context.Servicos.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        return servico is null ? NotFound() : Ok(servico);
    }

    [HttpPost]
    public async Task<IActionResult> Post(ServicoRequest request)
    {
        var servico = new Servico
        {
            Nome = request.Nome,
            Descricao = request.Descricao,
            Preco = request.Preco,
            TempoEstimadoMinutos = request.TempoEstimadoMinutos,
            Ativo = request.Ativo
        };
        _context.Servicos.Add(servico);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = servico.Id }, servico);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Put(Guid id, ServicoRequest request)
    {
        var servico = await _context.Servicos.FindAsync(id);
        if (servico is null)
            return NotFound();

        servico.Nome = request.Nome;
        servico.Descricao = request.Descricao;
        servico.Preco = request.Preco;
        servico.TempoEstimadoMinutos = request.TempoEstimadoMinutos;
        servico.Ativo = request.Ativo;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var servico = await _context.Servicos.FindAsync(id);
        if (servico is null)
            return NotFound();

        _context.Servicos.Remove(servico);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
