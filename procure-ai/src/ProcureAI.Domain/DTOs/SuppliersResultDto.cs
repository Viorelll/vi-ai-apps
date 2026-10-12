namespace ProcureAI.Domain.DTOs;

public sealed record SuppliersResultDto(IReadOnlyList<OfferDto> Offers, IReadOnlyList<string> Unknowns);
