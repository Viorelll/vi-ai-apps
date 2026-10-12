namespace ProcureAI.Domain.DTOs;

public sealed record CriteriaDto(string Currency, decimal Quantity, string Unit, int MaxDays);
