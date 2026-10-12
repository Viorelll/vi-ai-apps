namespace VideoSearchAI.Contracts.Dto;

public sealed record EvidenceDto(
    string SourceId,
    long StartMs,
    long EndMs,
    string Transcript,
    string? VisualText,
    FrameDto[] Frames
);
