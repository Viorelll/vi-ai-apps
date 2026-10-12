namespace IncidentAI.Domain.Entities;

public sealed class ReleaseSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IncidentId { get; set; }
    public required string Label { get; set; }
    public DateTimeOffset? DeployedAtUtc { get; set; }
}
