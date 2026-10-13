namespace BuildBudgetAI.Domain.Dto;

public sealed record OfferLineInput(
    string MaterialLabel,
    string Unit,
    decimal Price,
    decimal? Transport,
    decimal? PackQuantity,
    bool Confirmed,
    IReadOnlyList<EvidenceDto>? Evidence
);
