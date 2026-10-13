namespace BuildBudgetAI.Domain.Entities;

public sealed class Citation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public Guid? DocumentId { get; set; }
    public Guid? ChunkId { get; set; }
    public int Generation { get; set; }
    public string Label { get; set; } = "";
    public string Excerpt { get; set; } = "";
    public int? PageNumber { get; set; }
}
