namespace MeetingAI.Domain.Entities;

public sealed class Meeting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Meeting recording";
    public string? Sha256 { get; set; }
    public string? AudioObjectKey { get; set; }
    public string? AudioContentType { get; set; }
    public long AudioSizeBytes { get; set; }
    public string State { get; set; } = "Ready";
    public string Summary { get; set; } = "";
    public long Revision { get; set; } = 1;
    public long TranscriptRevision { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<TranscriptSegment> Segments { get; set; } = [];
    public List<Decision> Decisions { get; set; } = [];
    public List<ActionItem> ActionItems { get; set; } = [];
}
