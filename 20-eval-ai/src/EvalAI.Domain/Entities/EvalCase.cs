namespace EvalAI.Domain.Entities;

public sealed class EvalCase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DatasetId { get; set; }
    public int Ordinal { get; set; }
    public string Name { get; set; } = ""; public string InputJson { get; set; } = "{}"; public string ExpectedJson { get; set; } = "{}"; public string EvidenceIdsJson { get; set; } = "[]";
}
