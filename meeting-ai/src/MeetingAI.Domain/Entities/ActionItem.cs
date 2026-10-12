namespace MeetingAI.Domain.Entities;

public sealed class ActionItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public string Text { get; set; } = "";
    public string? AssigneeLabel { get; set; }
    public string? DueText { get; set; }
    public List<Guid> SegmentIds { get; set; } = [];
}
