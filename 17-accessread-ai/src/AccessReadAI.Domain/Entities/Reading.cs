namespace AccessReadAI.Domain.Entities;

public sealed class Reading
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public string SourceLanguage { get; set; } = "ro";
    public string TargetLanguage { get; set; } = "en";
    public string State { get; set; } = "Uploaded";
    public long Revision { get; set; } = 1;
    public int Generation { get; set; }
    public string? ErrorCode { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public List<ReadingBlock> Blocks { get; set; } = [];
}
