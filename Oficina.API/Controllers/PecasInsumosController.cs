using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.API.Contracts;
using Oficina.Application.Common;
using Oficina.Application.Services;

namespace Oficina.API.Controllers;

[ApiController]
[Authorize]
[Route("api/pecas-insumos")]
public class PecasInsumosController : ControllerBase
{
    private readonly PecaInsumoService _service;

    public PecasInsumosController(PecaInsumoService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(await _service.ListarAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var peca = await _service.ObterPorIdAsync(id);
        return peca is null ? NotFound() : Ok(peca);
    }

    [HttpPost]
    public async Task<IActionResult> Post(PecaInsumoRequest request)
    {
        var resultado = await _service.CriarAsync(
            request.Nome,
            request.Codigo,
            request.PrecoUnitario,
            request.QuantidadeEstoque,
            request.EstoqueMinimo,
            request.Ativo);

        if (!resultado.Sucesso)
            return Responder(resultado);

        return CreatedAtAction(nameof(Get), new { id = resultado.Valor!.Id }, resultado.Valor);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Put(Guid id, PecaInsumoRequest request)
    {
        var resultado = await _service.AtualizarAsync(
            id,
            request.Nome,
            request.Codigo,
            request.PrecoUnitario,
            request.QuantidadeEstoque,
            request.EstoqueMinimo,
            request.Ativo);

        return Responder(resultado);
    }

    [HttpPatch("{id:guid}/estoque")]
    public async Task<IActionResult> ReporEstoque(Guid id, ReporEstoqueRequest request)
    {
        var resultado = await _service.ReporEstoqueAsync(id, request.Quantidade);
        return resultado.Sucesso ? Ok(resultado.Valor) : Responder(resultado);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var resultado = await _service.RemoverAsync(id);
        return Responder(resultado);
    }

    private IActionResult Responder(ResultadoOperacao resultado)
    {
        return resultado.Tipo switch
        {
            TipoResultado.Sucesso => NoContent(),
            TipoResultado.NaoEncontrado => NotFound(Mensagem(resultado)),
            TipoResultado.Conflito => Conflict(Mensagem(resultado)),
            TipoResultado.DadosInvalidos => BadRequest(Mensagem(resultado)),
            TipoResultado.NaoAutorizado => Unauthorized(Mensagem(resultado)),
            _ => BadRequest(Mensagem(resultado))
        };
    }

    private static object? Mensagem(ResultadoOperacao resultado)
    {
        return resultado.Mensagem is null ? null : new { message = resultado.Mensagem };
    }
}
