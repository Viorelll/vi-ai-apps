namespace MeetingAI.Domain.Entities;

public sealed class TranscriptSegment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public int Ordinal { get; set; }
    public long StartMs { get; set; }
    public long EndMs { get; set; }
    public string? SpeakerId { get; set; }
    public string? SpeakerLabel { get; set; }
    public string Text { get; set; } = "";
    public string RawText { get; set; } = "";
}
