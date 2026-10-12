namespace VideoSearchAI.Contracts.Dto;

public sealed record SegmentDto(
    Guid Id,
    Guid VideoId,
    Guid DocumentId,
    int Generation,
    int Ordinal,
    long StartMs,
    long EndMs,
    string Transcript,
    string? VisualText,
    string SourceId
);
