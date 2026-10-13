namespace VideoSearchAI.Contracts.Dto;

public sealed record CitationDto(
    string Label,
    Guid VideoId,
    Guid SegmentId,
    Guid DocumentId,
    int Generation,
    long StartMs,
    long EndMs,
    string Excerpt,
    string ContentUrl,
    string EvidenceKind
);
