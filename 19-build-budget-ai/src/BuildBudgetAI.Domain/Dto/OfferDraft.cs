namespace BuildBudgetAI.Domain.Dto;

public sealed record OfferDraft(
    string? SupplierLabel,
    string? Currency,
    string? TaxBasis,
    IReadOnlyList<OfferDraftLine> Lines,
    string Provider
);
