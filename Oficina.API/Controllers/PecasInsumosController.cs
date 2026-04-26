using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oficina.API.Contracts;
using Oficina.Domain.Entities;
using Oficina.Infrastructure.Persistence;

namespace Oficina.API.Controllers;

[ApiController]
[Authorize]
[Route("api/pecas-insumos")]
public class PecasInsumosController : ControllerBase
{
    private readonly OficinaDbContext _context;

    public PecasInsumosController(OficinaDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _context.PecasInsumos.AsNoTracking().ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var peca = await _context.PecasInsumos.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        return peca is null ? NotFound() : Ok(peca);
    }

    [HttpPost]
    public async Task<IActionResult> Post(PecaInsumoRequest request)
    {
        if (await _context.PecasInsumos.AnyAsync(item => item.Codigo == request.Codigo))
            return Conflict(new { message = "Ja existe uma peca/insumo com este codigo." });

        var peca = new PecaInsumo
        {
            Nome = request.Nome,
            Codigo = request.Codigo,
            PrecoUnitario = request.PrecoUnitario,
            QuantidadeEstoque = request.QuantidadeEstoque,
            EstoqueMinimo = request.EstoqueMinimo,
            Ativo = request.Ativo
        };
        _context.PecasInsumos.Add(peca);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = peca.Id }, peca);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Put(Guid id, PecaInsumoRequest request)
    {
        var peca = await _context.PecasInsumos.FindAsync(id);
        if (peca is null)
            return NotFound();

        if (await _context.PecasInsumos.AnyAsync(item => item.Id != id && item.Codigo == request.Codigo))
            return Conflict(new { message = "Ja existe outra peca/insumo com este codigo." });

        peca.Nome = request.Nome;
        peca.Codigo = request.Codigo;
        peca.PrecoUnitario = request.PrecoUnitario;
        peca.QuantidadeEstoque = request.QuantidadeEstoque;
        peca.EstoqueMinimo = request.EstoqueMinimo;
        peca.Ativo = request.Ativo;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:guid}/estoque")]
    public async Task<IActionResult> ReporEstoque(Guid id, ReporEstoqueRequest request)
    {
        var peca = await _context.PecasInsumos.FindAsync(id);
        if (peca is null)
            return NotFound();

        peca.ReporEstoque(request.Quantidade);
        await _context.SaveChangesAsync();
        return Ok(peca);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var peca = await _context.PecasInsumos.FindAsync(id);
        if (peca is null)
            return NotFound();

        _context.PecasInsumos.Remove(peca);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
