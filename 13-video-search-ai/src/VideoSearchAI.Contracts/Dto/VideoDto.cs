namespace VideoSearchAI.Contracts.Dto;

public sealed record VideoDto(
    Guid Id,
    Guid DocumentId,
    string Title,
    long DurationMs,
    string State,
    long Revision,
    string? ErrorCode
);
