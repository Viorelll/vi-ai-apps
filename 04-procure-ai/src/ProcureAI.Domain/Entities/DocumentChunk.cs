namespace ProcureAI.Domain.Entities;

public sealed class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public int Generation { get; set; } = 1;
    public int Ordinal { get; set; }
    public int? PageNumber { get; set; }
    public required string Text { get; set; }
}
