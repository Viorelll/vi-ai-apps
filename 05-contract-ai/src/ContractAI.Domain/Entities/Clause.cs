namespace ContractAI.Domain.Entities;

public sealed class Clause
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VersionId { get; set; }
    public Guid ChunkId { get; set; }
    public int Ordinal { get; set; }
    public string? Heading { get; set; }
    public string Text { get; set; } = "";
    public int? PageNumber { get; set; }
    public int SpanStart { get; set; }
    public int SpanLength { get; set; }
}
