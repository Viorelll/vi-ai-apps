namespace InvoiceAI.Contracts.Dto;

public sealed record LineDto(int Ordinal, string? Description, decimal? Quantity, decimal? UnitPrice, decimal? Amount, string? Currency, int? PageNumber);
