namespace AccessReadAI.Domain.Entities;

public sealed class NarrationSegment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReadingId { get; set; }
    public Guid VariantId { get; set; }
    public string ObjectKey { get; set; } = "";
    public string TextHash { get; set; } = "";
    public string ContentType { get; set; } = "audio/wav";
    public string Voice { get; set; } = "";
    public int Ordinal { get; set; }
    public bool Stale { get; set; }
}
