using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oficina.API.Contracts;
using Oficina.API.Security;
using Oficina.Application.Common;
using Oficina.Application.Models;
using Oficina.Application.Services;

namespace Oficina.API.Controllers;

[ApiController]
[Route("api/ordens-servico")]
public class OrdensServicoController : ControllerBase
{
    private readonly OrdemServicoService _service;
    private readonly IConfiguration _configuration;

    public OrdensServicoController(OrdemServicoService service, IConfiguration configuration)
    {
        _service = service;
        _configuration = configuration;
    }

    [Authorize(Policy = AutenticacaoExtensions.Administrador)]
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var ordens = await _service.ListarResumoAsync();
        return Ok(ordens.Select(ordem => new OrdemServicoResumoResponse(
            ordem.Id,
            ordem.Numero,
            ordem.Status.ToDisplayName(),
            ordem.ValorTotal,
            ordem.CriadaEm)));
    }

    [Authorize(Policy = AutenticacaoExtensions.ConsultarOrdem)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var resultado = await ConsultarComAcessoAsync(id);
        return resultado.Sucesso ? Ok(OrdemServicoDetalheResponse.FromEntity(resultado.Valor!)) : Responder(resultado);
    }

    [Authorize(Policy = AutenticacaoExtensions.Administrador)]
    [HttpGet("{id:guid}/historico")]
    public async Task<IActionResult> Historico(Guid id)
    {
        var ordem = await _service.ObterDetalhadaAsync(id);
        return ordem is null
            ? NotFound()
            : Ok(ordem.HistoricoStatus.OrderBy(periodo => periodo.Sequencia).Select(HistoricoStatusResponse.FromEntity));
    }

    [Authorize(Policy = AutenticacaoExtensions.Administrador)]
    [HttpPost]
    public async Task<IActionResult> Post(CriarOrdemServicoRequest request)
    {
        var cliente = MontarClienteDaOrdem(request);
        if (cliente is null)
            return BadRequest(new { message = "Informe os dados do cliente ou o CPF/CNPJ de um cliente ja cadastrado." });

        var veiculo = new VeiculoOrdemInput(
            request.Veiculo.Placa,
            request.Veiculo.Marca,
            request.Veiculo.Modelo,
            request.Veiculo.Ano);

        var pecas = request.Pecas
            .Select(peca => new PecaOrdemInput(peca.PecaInsumoId, peca.Quantidade))
            .ToList();

        var resultado = await _service.CriarAsync(
            cliente,
            veiculo,
            request.ServicosIds,
            pecas,
            request.Observacoes,
            request.IniciarEmDiagnostico);

        if (!resultado.Sucesso)
            return Responder(resultado);

        return CreatedAtAction(
            nameof(Get),
            new { id = resultado.Valor!.Id },
            OrdemServicoDetalheResponse.FromEntity(resultado.Valor));
    }

    [Authorize(Policy = AutenticacaoExtensions.Administrador)]
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> AlterarStatus(Guid id, AlterarStatusRequest request)
    {
        var resultado = await _service.AlterarStatusAsync(id, request.Status);
        return resultado.Sucesso
            ? Ok(OrdemServicoDetalheResponse.FromEntity(resultado.Valor!))
            : Responder(resultado);
    }

    [Authorize(Policy = AutenticacaoExtensions.ConsultarOrdem)]
    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> ConsultarStatus(Guid id)
    {
        var resultado = await ConsultarComAcessoAsync(id);
        return resultado.Sucesso
            ? Ok(OrdemServicoDetalheResponse.StatusFromEntity(resultado.Valor!))
            : Responder(resultado);
    }

    [Authorize(Policy = AutenticacaoExtensions.ClienteAutorizado)]
    [HttpPost("{id:guid}/aprovar")]
    public async Task<IActionResult> Aprovar(Guid id)
    {
        var resultado = await _service.AprovarAsync(id, User.ClienteId());
        return resultado.Sucesso
            ? Ok(OrdemServicoDetalheResponse.FromEntity(resultado.Valor!))
            : Responder(resultado);
    }

    [AllowAnonymous]
    [HttpPost("orcamentos/notificacoes")]
    public async Task<IActionResult> ReceberDecisaoOrcamento(
        DecisaoOrcamentoRequest request,
        [FromHeader(Name = "X-Webhook-Token")] string? token)
    {
        if (!TokenExternoValido(token))
            return Unauthorized(new { message = "Token da notificacao externa invalido." });

        var resultado = await _service.RegistrarDecisaoOrcamentoAsync(
            request.OrdemServicoId,
            request.Decisao,
            request.CpfCnpjCliente,
            request.Motivo);

        return resultado.Sucesso
            ? Ok(OrdemServicoDetalheResponse.FromEntity(resultado.Valor!))
            : Responder(resultado);
    }

    [Authorize(Policy = AutenticacaoExtensions.ConsultarOrdem)]
    [HttpGet("consulta/{id:guid}")]
    public async Task<IActionResult> ConsultarCliente(Guid id)
    {
        var resultado = await ConsultarComAcessoAsync(id);
        return resultado.Sucesso
            ? Ok(OrdemServicoDetalheResponse.FromEntity(resultado.Valor!))
            : Responder(resultado);
    }

    [Authorize(Policy = AutenticacaoExtensions.Administrador)]
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

    private async Task<ResultadoOperacao<Oficina.Domain.Entities.OrdemServico>> ConsultarComAcessoAsync(Guid id)
    {
        if (!User.EhAdministrador()) return await _service.ConsultarClienteAsync(id, User.ClienteId());
        var ordem = await _service.ObterDetalhadaAsync(id);
        return ordem is null ? ResultadoOperacao<Oficina.Domain.Entities.OrdemServico>.NaoEncontrado()
            : ResultadoOperacao<Oficina.Domain.Entities.OrdemServico>.Ok(ordem);
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

    private static ClienteOrdemInput? MontarClienteDaOrdem(CriarOrdemServicoRequest request)
    {
        if (request.Cliente is not null)
        {
            return new ClienteOrdemInput(
                request.Cliente.CpfCnpj,
                request.Cliente.Nome,
                request.Cliente.Telefone,
                request.Cliente.Email);
        }

        return string.IsNullOrWhiteSpace(request.CpfCnpjCliente)
            ? null
            : new ClienteOrdemInput(request.CpfCnpjCliente, null, null, null);
    }

    private bool TokenExternoValido(string? token)
    {
        var tokenEsperado = _configuration["ExternalIntegrations:BudgetToken"];
        return !string.IsNullOrWhiteSpace(tokenEsperado) && token == tokenEsperado;
    }
}
