using System.ComponentModel.DataAnnotations;
using Oficina.Domain.Enums;
using System.Text.Json.Serialization;

namespace Oficina.API.Contracts;

public record LoginRequest([Required] string Usuario, [Required] string Senha);

public record AlterarStatusClienteRequest([Required] bool? Ativo);

public record ClienteRequest(
    [Required, MaxLength(160)] string Nome,
    [Required, MaxLength(18)] string CpfCnpj,
    [MaxLength(30)] string Telefone,
    [EmailAddress, MaxLength(160)] string Email);

public record VeiculoRequest(
    [Required, MaxLength(10)] string Placa,
    [Required, MaxLength(80)] string Marca,
    [Required, MaxLength(80)] string Modelo,
    [Range(1900, 2100)] int Ano,
    [Required] Guid ClienteId);

public record VeiculoOrdemServicoRequest(
    [Required, MaxLength(10)] string Placa,
    [Required, MaxLength(80)] string Marca,
    [Required, MaxLength(80)] string Modelo,
    [Range(1900, 2100)] int Ano);

public record ClienteOrdemServicoRequest(
    [Required, MaxLength(160)] string Nome,
    [Required, MaxLength(18)] string CpfCnpj,
    [MaxLength(30)] string? Telefone,
    [EmailAddress, MaxLength(160)] string? Email);

public record ServicoRequest(
    [Required, MaxLength(120)] string Nome,
    [MaxLength(500)] string Descricao,
    [Range(0.01, 999999)] decimal Preco,
    [Range(1, 100000)] int TempoEstimadoMinutos,
    bool Ativo = true);

public record PecaInsumoRequest(
    [Required, MaxLength(120)] string Nome,
    [Required, MaxLength(60)] string Codigo,
    [Range(0.01, 999999)] decimal PrecoUnitario,
    [Range(0, 1000000)] int QuantidadeEstoque,
    [Range(0, 1000000)] int EstoqueMinimo,
    bool Ativo = true);

public record ReporEstoqueRequest([Range(1, 1000000)] int Quantidade);

public record CriarOrdemServicoRequest(
    ClienteOrdemServicoRequest? Cliente,
    [MaxLength(18)] string? CpfCnpjCliente,
    [Required] VeiculoOrdemServicoRequest Veiculo,
    [Required] List<Guid> ServicosIds,
    [Required] List<PecaOrdemRequest> Pecas,
    [MaxLength(1000)] string? Observacoes,
    bool IniciarEmDiagnostico = false);

public record PecaOrdemRequest([Required] Guid PecaInsumoId, [Range(1, 100000)] int Quantidade);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CriarMinhaOrdemServicoRequest(
    [Required] VeiculoOrdemServicoRequest Veiculo,
    [Required] List<Guid> ServicosIds,
    [Required] List<PecaOrdemRequest> Pecas,
    [MaxLength(1000)] string? Observacoes);

public record AlterarStatusRequest([Required] StatusOrdemServico Status);

public record DecisaoOrcamentoRequest(
    [Required] Guid OrdemServicoId,
    [Required, MaxLength(20)] string Decisao,
    [Required, MaxLength(18)] string CpfCnpjCliente,
    [MaxLength(500)] string? Motivo);
