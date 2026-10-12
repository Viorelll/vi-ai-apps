namespace IncidentAI.Domain.Entities;

public sealed class LogSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IncidentId { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public required string Sha256 { get; set; }
    public required string ObjectKey { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
