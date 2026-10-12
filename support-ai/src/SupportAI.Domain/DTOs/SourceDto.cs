namespace SupportAI.Domain.DTOs;

public sealed record SourceDto(string Label, Guid DocumentId, Guid ChunkId, int Generation, string FileName, string Text, int? PageNumber, long? StartMs, long? EndMs, bool IsPolicy = false);
