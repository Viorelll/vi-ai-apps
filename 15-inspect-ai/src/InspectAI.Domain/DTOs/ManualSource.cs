namespace InspectAI.Domain.DTOs;

public sealed record ManualSource(
    Guid ChunkId,
    Guid DocumentId,
    int Generation,
    string Text,
    int? PageNumber,
    string Sha256
);
