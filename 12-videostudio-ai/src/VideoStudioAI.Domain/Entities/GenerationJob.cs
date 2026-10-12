namespace VideoStudioAI.Domain.Entities;

public sealed class GenerationJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid RequestId { get; set; }
    public string Kind { get; set; } = "Storyboard";
    public string State { get; set; } = "Queued";
    public string Phase { get; set; } = "Pending";
    public string InputJson { get; set; } = "{}";
    public string? ProviderId { get; set; }
    public Guid? ParentClipId { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public DateTimeOffset NextPollAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int Attempt { get; set; }
    public int PollCount { get; set; }
    public string? ErrorCode { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
}
