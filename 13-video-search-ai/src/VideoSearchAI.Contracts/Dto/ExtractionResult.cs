namespace VideoSearchAI.Contracts.Dto;

public sealed record ExtractionResult(
    string Status,
    string? OperationUrl,
    string? RawJson,
    EvidenceDto[] Sources
);
