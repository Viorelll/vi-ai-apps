namespace AccessReadAI.Domain.Entities;

public sealed class IngestionJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? DocumentId { get; set; }
    public Guid? TargetId { get; set; }
    public string Kind { get; set; } = "Extract";
    public string PipelineVersion { get; set; } = "reading-v1";
    public string State { get; set; } = "Queued";
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int Attempt { get; set; }
    public string? OperationUrl { get; set; }
    public string? ErrorCode { get; set; }
    public string InputJson { get; set; } = "{}";
}
