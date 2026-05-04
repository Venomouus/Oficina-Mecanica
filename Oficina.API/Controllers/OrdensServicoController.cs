using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.API.Contracts;
using Oficina.Application.Common;
using Oficina.Application.Models;
using Oficina.Application.Services;

namespace Oficina.API.Controllers;

[ApiController]
[Route("api/ordens-servico")]
public class OrdensServicoController : ControllerBase
{
    private readonly OrdemServicoService _service;

    public OrdensServicoController(OrdemServicoService service)
    {
        _service = service;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var ordens = await _service.ListarResumoAsync();
        return Ok(ordens.Select(ordem => new OrdemServicoResumoResponse(
            ordem.Id,
            ordem.Numero,
            ordem.Status.ToString(),
            ordem.ValorTotal,
            ordem.CriadaEm)));
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var ordem = await _service.ObterDetalhadaAsync(id);
        return ordem is null ? NotFound() : Ok(OrdemServicoDetalheResponse.FromEntity(ordem));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Post(CriarOrdemServicoRequest request)
    {
        var veiculo = new VeiculoOrdemInput(
            request.Veiculo.Placa,
            request.Veiculo.Marca,
            request.Veiculo.Modelo,
            request.Veiculo.Ano);

        var pecas = request.Pecas
            .Select(peca => new PecaOrdemInput(peca.PecaInsumoId, peca.Quantidade))
            .ToList();

        var resultado = await _service.CriarAsync(
            request.CpfCnpjCliente,
            veiculo,
            request.ServicosIds,
            pecas,
            request.Observacoes);

        if (!resultado.Sucesso)
            return Responder(resultado);

        return CreatedAtAction(
            nameof(Get),
            new { id = resultado.Valor!.Id },
            OrdemServicoDetalheResponse.FromEntity(resultado.Valor));
    }

    [Authorize]
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> AlterarStatus(Guid id, AlterarStatusRequest request)
    {
        var resultado = await _service.AlterarStatusAsync(id, request.Status);
        return resultado.Sucesso
            ? Ok(OrdemServicoDetalheResponse.FromEntity(resultado.Valor!))
            : Responder(resultado);
    }

    [AllowAnonymous]
    [HttpPost("{id:guid}/aprovar")]
    public async Task<IActionResult> Aprovar(Guid id, [FromQuery] string cpfCnpj)
    {
        var resultado = await _service.AprovarAsync(id, cpfCnpj);
        return resultado.Sucesso
            ? Ok(OrdemServicoDetalheResponse.FromEntity(resultado.Valor!))
            : Responder(resultado);
    }

    [AllowAnonymous]
    [HttpGet("consulta/{id:guid}")]
    public async Task<IActionResult> ConsultarCliente(Guid id, [FromQuery] string cpfCnpj)
    {
        var resultado = await _service.ConsultarClienteAsync(id, cpfCnpj);
        return resultado.Sucesso
            ? Ok(OrdemServicoDetalheResponse.FromEntity(resultado.Valor!))
            : Responder(resultado);
    }

    [Authorize]
    [HttpGet("metricas/tempo-medio")]
    public async Task<IActionResult> TempoMedioExecucao()
    {
        var metricas = await _service.CalcularTempoMedioExecucaoAsync();

        return Ok(new
        {
            quantidadeOrdensFinalizadas = metricas.QuantidadeOrdensFinalizadas,
            tempoMedioExecucaoMinutos = metricas.TempoMedioExecucaoMinutos
        });
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
