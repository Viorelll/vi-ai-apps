namespace VideoSearchAI.Domain.Entities;

public sealed class VideoEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public string Title { get; set; } = "";
    public string State { get; set; } = "Uploaded";
    public long Revision { get; set; } = 1;
    public string? ErrorCode { get; set; }
}
