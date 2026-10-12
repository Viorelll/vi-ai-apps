namespace VideoStudioAI.Contracts.Dto;

public sealed record JobDto(
    Guid Id,
    string Kind,
    string State,
    string? ProviderId,
    string? ErrorCode,
    int Attempt,
    long InputTokens,
    long OutputTokens
);
