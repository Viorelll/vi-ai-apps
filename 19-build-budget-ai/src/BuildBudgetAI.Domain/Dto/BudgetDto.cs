namespace BuildBudgetAI.Domain.Dto;

public sealed record BudgetDto(
    Guid Id,
    long Revision,
    string TotalDecimal,
    string Currency,
    bool Incomplete,
    bool Stale,
    IReadOnlyList<BudgetLineDto> Lines
);
