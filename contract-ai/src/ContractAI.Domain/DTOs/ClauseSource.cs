namespace ContractAI.Domain.DTOs;

public sealed record ClauseSource(
    Guid Id,
    Guid VersionId,
    Guid DocumentId,
    Guid ChunkId,
    int Generation,
    int? PageNumber,
    string Text,
    int Ordinal = 0
);
