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
public class VeiculosController : ControllerBase
{
    private readonly OficinaDbContext _context;

    public VeiculosController(OficinaDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _context.Veiculos.AsNoTracking().Include(item => item.Cliente).ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var veiculo = await _context.Veiculos.AsNoTracking().Include(item => item.Cliente).FirstOrDefaultAsync(item => item.Id == id);
        return veiculo is null ? NotFound() : Ok(veiculo);
    }

    [HttpPost]
    public async Task<IActionResult> Post(VeiculoRequest request)
    {
        var validation = await ValidateRequest(request);
        if (validation is not null)
            return validation;

        var veiculo = new Veiculo
        {
            Placa = PlacaValidator.Normalize(request.Placa),
            Marca = request.Marca,
            Modelo = request.Modelo,
            Ano = request.Ano,
            ClienteId = request.ClienteId
        };

        _context.Veiculos.Add(veiculo);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = veiculo.Id }, veiculo);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Put(Guid id, VeiculoRequest request)
    {
        var veiculo = await _context.Veiculos.FindAsync(id);
        if (veiculo is null)
            return NotFound();

        var validation = await ValidateRequest(request, id);
        if (validation is not null)
            return validation;

        veiculo.Placa = PlacaValidator.Normalize(request.Placa);
        veiculo.Marca = request.Marca;
        veiculo.Modelo = request.Modelo;
        veiculo.Ano = request.Ano;
        veiculo.ClienteId = request.ClienteId;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var veiculo = await _context.Veiculos.FindAsync(id);
        if (veiculo is null)
            return NotFound();

        _context.Veiculos.Remove(veiculo);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult?> ValidateRequest(VeiculoRequest request, Guid? currentId = null)
    {
        if (!PlacaValidator.IsValid(request.Placa))
            return BadRequest(new { message = "Placa invalida." });

        if (!await _context.Clientes.AnyAsync(item => item.Id == request.ClienteId))
            return BadRequest(new { message = "Cliente nao encontrado." });

        var placa = PlacaValidator.Normalize(request.Placa);
        if (await _context.Veiculos.AnyAsync(item => item.Id != currentId && item.Placa == placa))
            return Conflict(new { message = "Veiculo ja cadastrado com esta placa." });

        return null;
    }
}
