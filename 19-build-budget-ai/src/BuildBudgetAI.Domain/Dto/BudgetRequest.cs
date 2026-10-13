namespace BuildBudgetAI.Domain.Dto;

public sealed record BudgetRequest(
    long Revision,
    IReadOnlyList<BudgetSelection> SelectedOfferLines
);
