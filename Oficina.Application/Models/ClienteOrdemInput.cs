namespace Oficina.Application.Models;

public record ClienteOrdemInput(
    string CpfCnpj,
    string? Nome,
    string? Telefone,
    string? Email);