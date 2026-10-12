namespace VideoStudioAI.Domain.Entities;

public sealed class IngestionJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public string Kind { get; set; } = "Extract";
    public string State { get; set; } = "Queued";
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public DateTimeOffset NextPollAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int Attempt { get; set; }
}
