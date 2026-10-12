namespace MeetingAI.Ai.Contracts;

public sealed record AnalyzeRequest(Guid MeetingId, string FileName, string? TranscriptText);
public sealed record AiSegment(string Id, long StartMs, long EndMs, string? SpeakerId, string Text);
public sealed record AiDecision(string Text, string[] SegmentIds);
public sealed record AiAction(string Text, string? AssigneeLabel, string? DueText, string[] SegmentIds);
public sealed record AnalyzeResponse(string Summary, AiSegment[] Segments, AiDecision[] Decisions, AiAction[] ActionItems, string Provider);
