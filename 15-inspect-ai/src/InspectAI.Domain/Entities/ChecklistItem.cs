namespace InspectAI.Domain.Entities;

public sealed class ChecklistItem
{
    public Guid Id { get; set; }
    public Guid InspectionId { get; set; }
    public Guid FindingId { get; set; }
    public string Text { get; set; } = "";
    public string EvidenceJson { get; set; } = "[]";
    public string Status { get; set; } = "proposed";
}
