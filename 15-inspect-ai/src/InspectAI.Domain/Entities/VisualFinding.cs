namespace InspectAI.Domain.Entities;

public sealed class VisualFinding
{
    public Guid Id { get; set; }
    public Guid InspectionId { get; set; }
    public Guid DocumentId { get; set; }
    public string Kind { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Reason { get; set; }
    public string? RegionJson { get; set; }
}
