using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.API.Contracts;
using Oficina.API.Security;
using Oficina.Application.Services;

namespace Oficina.API.Controllers;

[ApiController]
[Authorize(Policy = AutenticacaoExtensions.Administrador)]
[Route("api/[controller]")]
public class ServicosController : ControllerBase
{
    private readonly ServicoService _service;

    public ServicosController(ServicoService service)
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
        var servico = await _service.ObterPorIdAsync(id);

        if (servico == null)
            return NotFound();

        return Ok(servico);
    }

    [HttpPost]
    public async Task<IActionResult> Post(ServicoRequest request)
    {
        var servico = await _service.CriarAsync(
            request.Nome,
            request.Descricao,
            request.Preco,
            request.TempoEstimadoMinutos);

        return CreatedAtAction(nameof(Get), new { id = servico.Id }, servico);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(Guid id, ServicoRequest request)
    {
        await _service.AtualizarAsync(
            id,
            request.Nome,
            request.Descricao,
            request.Preco,
            request.TempoEstimadoMinutos);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.RemoverAsync(id);
        return NoContent();
    }
}
