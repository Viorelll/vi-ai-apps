namespace VideoStudioAI.Domain.Entities;

public sealed class RemixLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceClipId { get; set; }
    public Guid ResultClipId { get; set; }
    public string Prompt { get; set; } = "";
}
