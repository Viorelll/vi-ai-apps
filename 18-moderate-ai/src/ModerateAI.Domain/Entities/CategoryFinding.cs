namespace ModerateAI.Domain.Entities;

public sealed class CategoryFinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnalysisId { get; set; }
    public required string Category { get; set; }
    public int Severity { get; set; }
}
