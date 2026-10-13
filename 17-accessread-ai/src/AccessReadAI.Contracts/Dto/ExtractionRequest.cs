namespace AccessReadAI.Contracts;

public sealed record ExtractionRequest(
    Guid DocumentId,
    string ObjectKey,
    string ContentType,
    string? OperationUrl
);
