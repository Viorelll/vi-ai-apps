namespace BuildBudgetAI.Domain.Dto;

public sealed record OfferDraftLine(
    string? MaterialLabel,
    string? Unit,
    string? Price,
    string? Transport,
    string? PackQuantity,
    IReadOnlyList<string> SourceIds
);
