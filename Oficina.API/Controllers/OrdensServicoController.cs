using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oficina.API.Contracts;
using Oficina.Domain.Entities;
using Oficina.Domain.Enums;
using Oficina.Domain.Validation;
using Oficina.Infrastructure.Persistence;

namespace Oficina.API.Controllers;

[ApiController]
[Route("api/ordens-servico")]
public class OrdensServicoController : ControllerBase
{
    private readonly OficinaDbContext _context;

    public OrdensServicoController(OficinaDbContext context) => _context = context;

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var ordens = await _context.OrdensServico
            .AsNoTracking()
            .OrderByDescending(item => item.CriadaEm)
            .Select(item => new OrdemServicoResumoResponse(item.Id, item.Numero, item.Status.ToString(), item.ValorTotal, item.CriadaEm))
            .ToListAsync();

        return Ok(ordens);
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var ordem = await FindOrdem(id);
        return ordem is null ? NotFound() : Ok(OrdemServicoDetalheResponse.FromEntity(ordem));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Post(CriarOrdemServicoRequest request)
    {
        if (!DocumentoValidator.IsValid(request.CpfCnpjCliente))
            return BadRequest(new { message = "CPF/CNPJ invalido." });

        if (request.ServicosIds.Count == 0)
            return BadRequest(new { message = "Informe pelo menos um servico." });

        var documento = DocumentoValidator.Normalize(request.CpfCnpjCliente);
        var cliente = await _context.Clientes.FirstOrDefaultAsync(item => item.CpfCnpj == documento);
        if (cliente is null)
            return NotFound(new { message = "Cliente nao encontrado." });

        var veiculo = await GetOrCreateVeiculo(request.Veiculo, cliente.Id);
        if (veiculo is null)
            return BadRequest(new { message = "Dados do veiculo invalidos ou vinculados a outro cliente." });

        var servicos = await _context.Servicos
            .Where(item => request.ServicosIds.Contains(item.Id) && item.Ativo)
            .ToListAsync();

        if (servicos.Count != request.ServicosIds.Distinct().Count())
            return BadRequest(new { message = "Um ou mais servicos nao foram encontrados ou estao inativos." });

        var pecasIds = request.Pecas.Select(item => item.PecaInsumoId).Distinct().ToList();
        var pecas = await _context.PecasInsumos.Where(item => pecasIds.Contains(item.Id) && item.Ativo).ToListAsync();
        if (pecas.Count != pecasIds.Count)
            return BadRequest(new { message = "Uma ou mais pecas/insumos nao foram encontrados ou estao inativos." });

        var ordem = new OrdemServico
        {
            ClienteId = cliente.Id,
            VeiculoId = veiculo.Id,
            Observacoes = request.Observacoes
        };

        ordem.Servicos = servicos.Select(servico => new OrdemServicoServico
        {
            ServicoId = servico.Id,
            Nome = servico.Nome,
            ValorUnitario = servico.Preco,
            TempoEstimadoMinutos = servico.TempoEstimadoMinutos
        }).ToList();

        foreach (var item in request.Pecas)
        {
            var peca = pecas.Single(entity => entity.Id == item.PecaInsumoId);
            if (peca.QuantidadeEstoque < item.Quantidade)
                return BadRequest(new { message = $"Estoque insuficiente para {peca.Nome}." });

            ordem.Pecas.Add(new OrdemServicoPeca
            {
                PecaInsumoId = peca.Id,
                Nome = peca.Nome,
                Quantidade = item.Quantidade,
                ValorUnitario = peca.PrecoUnitario
            });
        }

        ordem.EnviarParaAprovacao();
        _context.OrdensServico.Add(ordem);
        await _context.SaveChangesAsync();

        var created = await FindOrdem(ordem.Id);
        return CreatedAtAction(nameof(Get), new { id = ordem.Id }, OrdemServicoDetalheResponse.FromEntity(created!));
    }

    [Authorize]
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> AlterarStatus(Guid id, AlterarStatusRequest request)
    {
        var ordem = await FindOrdem(id);
        if (ordem is null)
            return NotFound();

        try
        {
            switch (request.Status)
            {
                case StatusOrdemServico.EmDiagnostico:
                    ordem.IniciarDiagnostico();
                    break;
                case StatusOrdemServico.AguardandoAprovacao:
                    ordem.EnviarParaAprovacao();
                    break;
                case StatusOrdemServico.EmExecucao:
                    ordem.IniciarExecucao();
                    BaixarEstoque(ordem);
                    break;
                case StatusOrdemServico.Finalizada:
                    ordem.Finalizar();
                    break;
                case StatusOrdemServico.Entregue:
                    ordem.Entregar();
                    break;
                default:
                    return BadRequest(new { message = "Status informado nao pode ser aplicado manualmente." });
            }

            await _context.SaveChangesAsync();
            return Ok(OrdemServicoDetalheResponse.FromEntity(ordem));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("{id:guid}/aprovar")]
    public async Task<IActionResult> Aprovar(Guid id, [FromQuery] string cpfCnpj)
    {
        var ordem = await FindOrdem(id);
        if (ordem is null)
            return NotFound();

        if (!DocumentoValidator.IsValid(cpfCnpj) || ordem.Cliente?.CpfCnpj != DocumentoValidator.Normalize(cpfCnpj))
            return Unauthorized(new { message = "Documento nao confere com a ordem de servico." });

        try
        {
            ordem.Aprovar();
            BaixarEstoque(ordem);
            await _context.SaveChangesAsync();
            return Ok(OrdemServicoDetalheResponse.FromEntity(ordem));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpGet("consulta/{id:guid}")]
    public async Task<IActionResult> ConsultarCliente(Guid id, [FromQuery] string cpfCnpj)
    {
        var ordem = await FindOrdem(id);
        if (ordem is null)
            return NotFound();

        if (!DocumentoValidator.IsValid(cpfCnpj) || ordem.Cliente?.CpfCnpj != DocumentoValidator.Normalize(cpfCnpj))
            return Unauthorized(new { message = "Documento nao confere com a ordem de servico." });

        return Ok(OrdemServicoDetalheResponse.FromEntity(ordem));
    }

    [Authorize]
    [HttpGet("metricas/tempo-medio")]
    public async Task<IActionResult> TempoMedioExecucao()
    {
        var finalizadas = await _context.OrdensServico
            .AsNoTracking()
            .Where(item => item.IniciadaEm != null && item.FinalizadaEm != null)
            .Select(item => new { item.IniciadaEm, item.FinalizadaEm })
            .ToListAsync();

        var mediaMinutos = finalizadas.Count == 0
            ? 0
            : finalizadas.Average(item => (item.FinalizadaEm!.Value - item.IniciadaEm!.Value).TotalMinutes);

        return Ok(new
        {
            quantidadeOrdensFinalizadas = finalizadas.Count,
            tempoMedioExecucaoMinutos = Math.Round(mediaMinutos, 2)
        });
    }

    private async Task<OrdemServico?> FindOrdem(Guid id) => await _context.OrdensServico
        .Include(item => item.Cliente)
        .Include(item => item.Veiculo)
        .Include(item => item.Servicos)
        .Include(item => item.Pecas)
        .ThenInclude(item => item.PecaInsumo)
        .FirstOrDefaultAsync(item => item.Id == id);

    private async Task<Veiculo?> GetOrCreateVeiculo(VeiculoRequest request, Guid clienteId)
    {
        if (!PlacaValidator.IsValid(request.Placa))
            return null;

        var placa = PlacaValidator.Normalize(request.Placa);
        var veiculo = await _context.Veiculos.FirstOrDefaultAsync(item => item.Placa == placa);
        if (veiculo is not null)
            return veiculo.ClienteId == clienteId ? veiculo : null;

        veiculo = new Veiculo
        {
            ClienteId = clienteId,
            Placa = placa,
            Marca = request.Marca,
            Modelo = request.Modelo,
            Ano = request.Ano
        };
        _context.Veiculos.Add(veiculo);
        await _context.SaveChangesAsync();
        return veiculo;
    }

    private static void BaixarEstoque(OrdemServico ordem)
    {
        foreach (var item in ordem.Pecas)
            item.PecaInsumo?.BaixarEstoque(item.Quantidade);
    }
}
