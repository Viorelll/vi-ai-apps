namespace IncidentAI.Domain.Entities;

public sealed class Incident
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Service { get; set; }
    public DateTimeOffset FromUtc { get; set; }
    public DateTimeOffset ToUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<LogEvent> Events { get; set; } = [];
    public List<DiagnosisRun> Diagnoses { get; set; } = [];
    public List<ReleaseSnapshot> Releases { get; set; } = [];
    public List<LogSource> LogSources { get; set; } = [];
}
