using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.API.Contracts;
using Oficina.API.Security;
using Oficina.Application.Common;
using Oficina.Application.Models;
using Oficina.Application.Services;

namespace Oficina.API.Controllers;

[ApiController]
[Route("api/minhas-ordens-servico")]
[Authorize(Policy = AutenticacaoExtensions.ClienteAutorizado)]
public sealed class MinhasOrdensServicoController(OrdemServicoService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var ordens = await service.ListarPorClienteAsync(User.ClienteId());
        return Ok(ordens.Select(ordem => new OrdemServicoResumoResponse(ordem.Id, ordem.Numero,
            ordem.Status.ToDisplayName(), ordem.ValorTotal, ordem.CriadaEm)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id)
    {
        var resultado = await service.ConsultarClienteAsync(id, User.ClienteId());
        return resultado.Sucesso ? Ok(OrdemServicoDetalheResponse.FromEntity(resultado.Valor!)) : Erro(resultado);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(CriarMinhaOrdemServicoRequest request)
    {
        var resultado = await service.CriarParaClienteAsync(User.ClienteId(),
            new VeiculoOrdemInput(request.Veiculo.Placa, request.Veiculo.Marca, request.Veiculo.Modelo, request.Veiculo.Ano),
            request.ServicosIds, request.Pecas.Select(p => new PecaOrdemInput(p.PecaInsumoId, p.Quantidade)).ToList(), request.Observacoes);
        return resultado.Sucesso ? CreatedAtAction(nameof(Obter), new { id = resultado.Valor!.Id },
            OrdemServicoDetalheResponse.FromEntity(resultado.Valor)) : Erro(resultado);
    }

    [HttpPost("{id:guid}/aprovar")]
    public async Task<IActionResult> Aprovar(Guid id)
    {
        var resultado = await service.AprovarAsync(id, User.ClienteId());
        return resultado.Sucesso ? Ok(OrdemServicoDetalheResponse.FromEntity(resultado.Valor!)) : Erro(resultado);
    }

    private IActionResult Erro(ResultadoOperacao resultado) => resultado.Tipo switch
    {
        TipoResultado.NaoEncontrado => NotFound(),
        TipoResultado.NaoAutorizado => Unauthorized(),
        TipoResultado.Conflito => Conflict(new { message = resultado.Mensagem }),
        _ => BadRequest(new { message = resultado.Mensagem })
    };
}
