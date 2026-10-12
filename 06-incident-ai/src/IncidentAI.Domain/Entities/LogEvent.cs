namespace IncidentAI.Domain.Entities;

public sealed class LogEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IncidentId { get; set; }
    public Guid? LogSourceId { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public required string Severity { get; set; }
    public string? TraceId { get; set; }
    public required string MessageRedacted { get; set; }
    public required string SourceId { get; set; }
}
