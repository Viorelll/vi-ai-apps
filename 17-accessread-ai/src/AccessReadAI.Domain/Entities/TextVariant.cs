namespace AccessReadAI.Domain.Entities;

public sealed class TextVariant
{
    public bool Stale { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BlockId { get; set; }
    public string Kind { get; set; } = "simplified";
    public string Language { get; set; } = "ro";
    public string Text { get; set; } = "";
    public string TextHash { get; set; } = "";
    public string PromptVersion { get; set; } = "accessible-v1";
    public string Deployment { get; set; } = "";
    public List<NarrationSegment> Narration { get; set; } = [];
}
