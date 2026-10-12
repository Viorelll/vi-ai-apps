namespace IncidentAI.Domain.Entities;

public sealed class Hypothesis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DiagnosisRunId { get; set; }
    public int Rank { get; set; }
    public required string Claim { get; set; }
    public required string ConfidenceLabel { get; set; }
    public List<string> EvidenceIds { get; set; } = [];
    public List<string> Contradictions { get; set; } = [];
}
