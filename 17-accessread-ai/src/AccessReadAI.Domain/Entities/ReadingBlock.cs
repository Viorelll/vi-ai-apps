namespace AccessReadAI.Domain.Entities;

public sealed class ReadingBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReadingId { get; set; }
    public Guid ChunkId { get; set; }
    public int Ordinal { get; set; }
    public int PageNumber { get; set; }
    public string Kind { get; set; } = "text";
    public string SourceId { get; set; } = "";
    public string OriginalText { get; set; } = "";
    public string EvidenceJson { get; set; } = "{}";
    public string? ImageObjectKey { get; set; }
    public bool Completed { get; set; }
    public bool NeedsReview { get; set; }
    public string? Warning { get; set; }
    public List<TextVariant> Variants { get; set; } = [];
    public List<ImageDescription> ImageDescriptions { get; set; } = [];
}
