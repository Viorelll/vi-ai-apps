namespace VideoStudioAI.Contracts.Dto;

public sealed record DocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Status,
    string? ErrorCode,
    string ContentUrl
);
