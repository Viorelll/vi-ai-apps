namespace ModerateAI.Domain.Entities;

public sealed class SafetyAnalysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItemId { get; set; }
    public required string Provider { get; set; }
    public required string ApiVersion { get; set; }
    public required string OutputScale { get; set; }
    public required string RawObjectKey { get; set; }
    public bool Complete { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
