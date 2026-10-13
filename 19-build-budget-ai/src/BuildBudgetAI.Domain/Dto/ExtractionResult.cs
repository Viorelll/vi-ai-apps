namespace BuildBudgetAI.Domain.Dto;

public sealed record ExtractionResult(
    string Status,
    string? OperationUrl,
    IReadOnlyList<EvidenceDto> Sources
);
