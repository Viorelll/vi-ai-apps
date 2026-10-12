namespace VideoSearchAI.Contracts.Dto;

public sealed record ExtractionRequest(
    Guid OperationId,
    string ObjectKey,
    string Sha256,
    long DurationMs
);
