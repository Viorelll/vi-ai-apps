namespace ModerateAI.Domain.Dto;

public sealed record AnalysisRequest(
    Guid OperationId,
    string? Text,
    string? ImageObjectKey,
    string? ImageContentType
);
