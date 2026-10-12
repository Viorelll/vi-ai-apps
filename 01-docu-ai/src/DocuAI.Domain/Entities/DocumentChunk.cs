namespace DocuAI.Domain.Entities;

public sealed class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public int Generation { get; set; }
    public int Ordinal { get; set; }
    public required string Text { get; set; }
    public int? PageNumber { get; set; }
    public long? StartMs { get; set; }
    public long? EndMs { get; set; }
}
