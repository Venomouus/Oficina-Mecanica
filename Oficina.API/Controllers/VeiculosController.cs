using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.API.Contracts;
using Oficina.API.Security;
using Oficina.Application.Services;

namespace Oficina.API.Controllers;

[ApiController]
[Authorize(Policy = AutenticacaoExtensions.Administrador)]
[Route("api/[controller]")]
public class VeiculosController : ControllerBase
{
    private readonly VeiculoService _service;

    public VeiculosController(VeiculoService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(await _service.ListarAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var veiculo = await _service.ObterPorIdAsync(id);

        if (veiculo == null)
            return NotFound();

        return Ok(veiculo);
    }

    [HttpPost]
    public async Task<IActionResult> Post(VeiculoRequest request)
    {
        var veiculo = await _service.CriarAsync(
            request.ClienteId,
            request.Placa,
            request.Marca,
            request.Modelo,
            request.Ano);

        return CreatedAtAction(nameof(Get), new { id = veiculo.Id }, veiculo);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(Guid id, VeiculoRequest request)
    {
        await _service.AtualizarAsync(
            id,
            request.Placa,
            request.Marca,
            request.Modelo,
            request.Ano);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _service.RemoverAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
