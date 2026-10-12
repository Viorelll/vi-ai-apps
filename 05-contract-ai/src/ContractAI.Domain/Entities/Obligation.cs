namespace ContractAI.Domain.Entities;

public sealed class Obligation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VersionId { get; set; }
    public Guid ClauseId { get; set; }
    public string PartyText { get; set; } = "";
    public string ActionText { get; set; } = "";
    public DateOnly? DueDate { get; set; }
    public string? RelativeTerm { get; set; }
    public string EvidenceJson { get; set; } = "[]";
}
