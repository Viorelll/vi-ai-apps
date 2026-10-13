namespace BuildBudgetAI.Ai.Models;

public sealed record Answer(
    string Summary,
    IReadOnlyList<string> Unknowns,
    IReadOnlyList<string> CitationLabels
);
