namespace DocuAI.Domain.DTOs;

public sealed record CitationDto(
    string Label,
    Guid DocumentId,
    Guid ChunkId,
    string FileName,
    int? PageNumber,
    long? StartMs,
    long? EndMs,
    string Excerpt,
    string ContentUrl);
