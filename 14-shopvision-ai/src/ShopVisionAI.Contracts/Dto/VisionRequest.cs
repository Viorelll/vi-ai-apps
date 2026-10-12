namespace ShopVisionAI.Contracts.Dto;

public sealed record VisionRequest(
    Guid OperationId,
    string SourceId,
    string ObjectKey,
    string Mime,
    string? Question
);
