namespace ProcureAI.Domain.Entities;

public sealed class Recommendation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public required string SnapshotHash { get; set; }
    public required string ScoresJson { get; set; }
    public required string ReportJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
