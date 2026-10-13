namespace BuildBudgetAI.Domain.Dto;

public sealed record BudgetLineDto(
    Guid NeedId,
    Guid OfferLineId,
    string Material,
    string Unit,
    string AdjustedQuantity,
    string? UnitPrice,
    string? TotalDecimal,
    string Formula,
    IReadOnlyList<string> Unknowns
);
