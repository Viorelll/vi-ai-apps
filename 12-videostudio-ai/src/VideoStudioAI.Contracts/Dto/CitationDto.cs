namespace VideoStudioAI.Contracts.Dto;

public sealed record CitationDto(
    string Label,
    Guid DocumentId,
    Guid ChunkId,
    string FileName,
    string Excerpt,
    int? PageNumber,
    string ContentUrl
);
