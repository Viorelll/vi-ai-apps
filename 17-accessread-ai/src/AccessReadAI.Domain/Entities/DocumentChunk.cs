namespace AccessReadAI.Domain.Entities;

public sealed class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public int Generation { get; set; } = 1;
    public int Ordinal { get; set; }
    public int PageNumber { get; set; }
    public string SourceId { get; set; } = "";
    public string Kind { get; set; } = "text";
    public string Text { get; set; } = "";
    public string EvidenceJson { get; set; } = "{}";
    public string? ImageObjectKey { get; set; }
    public bool Uncertain { get; set; }
}
