namespace AccessReadAI.Domain.Entities;

public sealed class ImageDescription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BlockId { get; set; }
    public string Text { get; set; } = "unknown";
    public string EvidenceJson { get; set; } = "{}";
}
