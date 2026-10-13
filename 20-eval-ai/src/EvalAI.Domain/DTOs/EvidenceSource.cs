namespace EvalAI.Domain.DTOs;

public sealed record EvidenceSource(Guid ChunkId, Guid DocumentId, int Generation, string Text, string FileName, int? PageNumber);
