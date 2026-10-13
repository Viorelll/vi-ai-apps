namespace ModerateAI.Domain.Entities;

public sealed class ReviewDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItemId { get; set; }
    public long ExpectedRevision { get; set; }
    public required string Decision { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
