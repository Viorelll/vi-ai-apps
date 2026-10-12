namespace ShopVisionAI.Domain.Entities;

public sealed class IngestionJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TargetId { get; set; }
    public VisualSearch Target { get; set; } = null!;
    public Guid RequestId { get; set; }
    public string Status { get; set; } = "Queued";
    public int Attempt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string PipelineVersion { get; set; } = "vision-v1";
}
