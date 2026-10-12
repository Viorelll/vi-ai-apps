namespace InspectAI.Domain.Entities;

public sealed class InspectionSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InspectionId { get; set; }
    public Guid DocumentId { get; set; }
    public string Role { get; set; } = "";
}
