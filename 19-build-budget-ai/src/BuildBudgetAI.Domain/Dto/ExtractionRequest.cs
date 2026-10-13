namespace BuildBudgetAI.Domain.Dto;

public sealed record ExtractionRequest(
    Guid DocumentId,
    string ObjectKey,
    string ContentType,
    string? OperationUrl
);
