namespace BuildBudgetAI.Domain.Dto;

public sealed record GenerateRequest(
    string Question,
    string ContextJson,
    IReadOnlyList<EvidenceDto> Sources
);
