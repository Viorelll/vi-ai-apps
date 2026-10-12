namespace VideoStudioAI.Domain.Entities;

public sealed class VideoClip
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid JobId { get; set; }
    public string ProviderId { get; set; } = "";
    public string ObjectKey { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long SizeBytes { get; set; }
    public int DurationMs { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Prompt { get; set; } = "";
    public string Deployment { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
