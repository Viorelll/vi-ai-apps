namespace TutorAI.Domain.Entities;

public sealed class Citation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public Guid? DocumentId { get; set; }
    public Guid? ChunkId { get; set; }
    public Guid SourceId { get; set; }
    public string OfficialUrl { get; set; } = "";
    public string Excerpt { get; set; } = "";
    public int? PageNumber { get; set; }
    public int Generation { get; set; }
}
