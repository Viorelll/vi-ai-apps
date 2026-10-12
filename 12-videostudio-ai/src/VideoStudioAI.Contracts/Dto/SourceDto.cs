namespace VideoStudioAI.Contracts.Dto;

public sealed record SourceDto(
    Guid DocumentId,
    Guid ChunkId,
    string FileName,
    string Excerpt,
    int? PageNumber = null
);
