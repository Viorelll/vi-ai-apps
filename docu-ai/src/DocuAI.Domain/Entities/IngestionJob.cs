namespace DocuAI.Domain.Entities;

public sealed class IngestionJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public IngestionJobStatus Status { get; set; } = IngestionJobStatus.Queued;
    public int Attempt { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LeaseUntilUtc { get; set; }
}
