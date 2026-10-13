namespace VideoSearchAI.Domain.Entities;

public sealed class VideoSegment
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }
    public int Generation { get; set; }
    public int Ordinal { get; set; }
    public long StartMs { get; set; }
    public long EndMs { get; set; }
    public string Transcript { get; set; } = "";
    public string? VisualText { get; set; }
    public string SourceId { get; set; } = "";
}
