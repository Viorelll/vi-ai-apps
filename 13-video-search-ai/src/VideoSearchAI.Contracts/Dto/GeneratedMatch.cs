namespace VideoSearchAI.Contracts.Dto;

public sealed record GeneratedMatch(
    string CitationLabel,
    Guid VideoId,
    Guid SegmentId,
    string EvidenceKind
);
