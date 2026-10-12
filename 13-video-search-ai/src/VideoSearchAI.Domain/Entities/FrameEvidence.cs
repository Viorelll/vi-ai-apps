namespace VideoSearchAI.Domain.Entities;

public sealed class FrameEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SegmentId { get; set; }
    public long AtMs { get; set; }
    public string Description { get; set; } = "";
    public string? ObjectKey { get; set; }
}
