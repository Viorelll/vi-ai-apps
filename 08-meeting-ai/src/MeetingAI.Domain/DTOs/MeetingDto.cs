namespace MeetingAI.Domain.DTOs;

public sealed record SegmentDto(Guid Id, int Ordinal, long StartMs, long EndMs, string? SpeakerId, string? SpeakerLabel, string Text);
public sealed record DecisionDto(Guid Id, string Text, IReadOnlyList<Guid> SegmentIds);
public sealed record ActionItemDto(Guid Id, string Text, string? AssigneeLabel, string? DueText, IReadOnlyList<Guid> SegmentIds);
public sealed record MeetingDto(Guid Id, string Title, string State, string Summary, long Revision, long TranscriptRevision, DateTimeOffset CreatedAtUtc,
    IReadOnlyList<SegmentDto> Segments, IReadOnlyList<DecisionDto> Decisions, IReadOnlyList<ActionItemDto> ActionItems);
public sealed record CorrectionRequest(long Revision, IReadOnlyList<SegmentCorrection> Segments);
public sealed record SegmentCorrection(Guid Id, string Text, string? SpeakerLabel);
