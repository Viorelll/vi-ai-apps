namespace ProcureAI.Domain.Entities;

public sealed class DurableJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? DocumentId { get; set; }
    public Guid? RunId { get; set; }
    public required string Kind { get; set; }
    public string State { get; set; } = "Queued";
    public string PipelineVersion { get; set; } = "procure-v1";
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? ProviderOperation { get; set; }
    public int Attempt { get; set; }
    public string? ErrorCode { get; set; }
}
