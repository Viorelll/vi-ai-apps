namespace SupportAI.Domain.Entities;

public sealed class Citation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId
    {
        get; set;
    }
    public Guid? DocumentId
    {
        get; set;
    }
    public Guid? ChunkId
    {
        get; set;
    }
    public required string Label
    {
        get; set;
    }
    public required string Excerpt
    {
        get; set;
    }
    public required string FileName
    {
        get; set;
    }
    public int Generation
    {
        get; set;
    }
    public int? PageNumber
    {
        get; set;
    }
    public long? StartMs
    {
        get; set;
    }
    public long? EndMs
    {
        get; set;
    }
}
