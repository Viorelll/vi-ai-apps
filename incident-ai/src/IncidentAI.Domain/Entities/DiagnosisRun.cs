namespace IncidentAI.Domain.Entities;

public sealed class DiagnosisRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IncidentId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public required string State { get; set; }
    public required string Summary { get; set; }
    public List<Hypothesis> Hypotheses { get; set; } = [];
    public List<string> NextChecks { get; set; } = [];
}
