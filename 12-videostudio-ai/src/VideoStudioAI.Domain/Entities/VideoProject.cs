namespace VideoStudioAI.Domain.Entities;

public sealed class VideoProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Brief { get; set; } = "";
    public string State { get; set; } = "Created";
    public long Revision { get; set; }
    public string? StoryboardJson { get; set; }
    public bool Confirmed { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
