namespace VideoSearchAI.Contracts.Dto;

public sealed record IndexRequest(
    Guid VideoId,
    Guid DocumentId,
    int Generation,
    string Title,
    SegmentDto[] Segments
);
