namespace ContractAI.Domain.DTOs;

public sealed record ExtractionResult(
    string Status,
    string? OperationUrl,
    ExtractedPage[]? Pages = null,
    string? ErrorCode = null,
    string? RawJson = null
);
