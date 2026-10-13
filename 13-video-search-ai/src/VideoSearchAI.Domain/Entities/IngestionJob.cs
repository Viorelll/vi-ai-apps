namespace VideoSearchAI.Domain.Entities;

public sealed class IngestionJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? DocumentId { get; set; }
    public Guid TargetId { get; set; }
    public string Kind { get; set; } = "Index";
    public string Status { get; set; } = "Queued";
    public string Step { get; set; } = "Start";
    public string PipelineVersion { get; set; } = "temporal-v1";
    public int Generation { get; set; }
    public int Attempt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? ProviderOperation { get; set; }
    public string? ResultObjectKey { get; set; }
    public string? ErrorCode { get; set; }
}
