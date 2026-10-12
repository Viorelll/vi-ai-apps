namespace SupportAI.Domain.DTOs;

public sealed record CitationDto(string Label, Guid? DocumentId, Guid? ChunkId, string FileName, string Excerpt, int? PageNumber, long? StartMs, long? EndMs, string? ContentUrl);
