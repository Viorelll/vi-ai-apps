namespace MeetingAI.Domain.Entities;

public sealed class Decision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public string Text { get; set; } = "";
    public List<Guid> SegmentIds { get; set; } = [];
}
