namespace VideoSearchAI.Domain.Entities;

public sealed class TemporalCitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public Guid? SegmentId { get; set; }
    public Guid VideoId { get; set; }
    public Guid DocumentId { get; set; }
    public int Generation { get; set; }
    public long StartMs { get; set; }
    public long EndMs { get; set; }
    public string Label { get; set; } = "";
    public string Excerpt { get; set; } = "";
}
