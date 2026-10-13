namespace BuildBudgetAI.Domain.Dto;

public sealed record OfferInput(
    long Revision,
    string SupplierLabel,
    string Currency,
    string TaxBasis,
    Guid? DocumentId,
    IReadOnlyList<OfferLineInput> Lines
);
