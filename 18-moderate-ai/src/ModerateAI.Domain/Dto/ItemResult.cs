namespace ModerateAI.Domain.Dto;

public sealed record ItemResult(
    Guid Id,
    long Revision,
    string State,
    bool Stale,
    string? ErrorCode,
    string? Explanation,
    DateTimeOffset CreatedAtUtc,
    string? Text,
    Guid? ImageDocumentId,
    object? Analysis,
    object? Policy,
    IReadOnlyList<Entities.ReviewDecision> Reviews
);
