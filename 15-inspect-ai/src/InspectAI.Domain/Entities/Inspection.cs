namespace InspectAI.Domain.Entities;

public sealed class Inspection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EquipmentLabel { get; set; } = "";
    public string EquipmentModel { get; set; } = "";
    public string ManualVersion { get; set; } = "";
    public string State { get; set; } = "Created";
    public int Revision { get; set; } = 1;
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
