namespace BuildBudgetAI.Domain.Dto;

public sealed record OfferExtractionRequest(IReadOnlyList<EvidenceDto> Sources);
