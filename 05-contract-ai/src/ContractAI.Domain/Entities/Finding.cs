namespace ContractAI.Domain.Entities;

public sealed class Finding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VersionId { get; set; }
    public Guid? ClauseId { get; set; }
    public string Code { get; set; } = "";
    public string Severity { get; set; } = "needs_review";
    public string Explanation { get; set; } = "";
    public string EvidenceJson { get; set; } = "[]";
}
