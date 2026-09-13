using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.API.Contracts;
using Oficina.Application.Services;

namespace Oficina.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ClientesController : ControllerBase
{
    private readonly ClienteService _service;

    public ClientesController(ClienteService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var clientes = await _service.ListarAsync();
        return Ok(clientes.Select(ClienteResponse.FromEntity));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var cliente = await _service.ObterPorIdAsync(id);

        if (cliente == null)
            return NotFound();

        return Ok(ClienteResponse.FromEntity(cliente));
    }

    [HttpPost]
    public async Task<IActionResult> Post(ClienteRequest request)
    {
        try
        {
            var cliente = await _service.CriarAsync(
                request.Nome,
                request.CpfCnpj,
                request.Telefone,
                request.Email);

            return CreatedAtAction(nameof(Get), new { id = cliente.Id }, ClienteResponse.FromEntity(cliente));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(Guid id, ClienteRequest request)
    {
        await _service.AtualizarAsync(
            id,
            request.Nome,
            request.Telefone,
            request.Email);

        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> AlterarStatus(Guid id, AlterarStatusClienteRequest request)
    {
        var resultado = await _service.AlterarStatusAsync(id, request.Ativo!.Value);
        return resultado.Sucesso ? Ok(ClienteResponse.FromEntity(resultado.Valor!)) : NotFound();
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
