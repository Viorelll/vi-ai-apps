namespace BuildBudgetAI.Domain.Dto;

public sealed record GenerateResult(
    string Summary,
    IReadOnlyList<string> Unknowns,
    IReadOnlyList<string> CitationLabels,
    string Deployment,
    long InputTokens,
    long OutputTokens
);
