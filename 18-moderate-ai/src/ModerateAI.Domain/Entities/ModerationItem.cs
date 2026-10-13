namespace ModerateAI.Domain.Entities;

public sealed class ModerationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public long Revision { get; set; }
    public string? Text { get; set; }
    public Guid? ImageDocumentId { get; set; }
    public required string InputHash { get; set; }
    public string State { get; set; } = "Submitted";
    public string? ErrorCode { get; set; }
    public string? Explanation { get; set; }
    public bool Stale { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
