namespace VideoSearchAI.Contracts.Dto;

public sealed record RetrievalHit(Guid SegmentId, Guid DocumentId, int Generation, double Score);
