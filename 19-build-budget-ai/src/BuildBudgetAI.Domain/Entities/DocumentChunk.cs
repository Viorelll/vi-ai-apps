namespace BuildBudgetAI.Domain.Entities;

public sealed class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public int Generation { get; set; }
    public int Ordinal { get; set; }
    public string Text { get; set; } = "";
    public int? PageNumber { get; set; }
}
