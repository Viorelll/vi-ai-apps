namespace ProcureAI.Domain.Entities;

public sealed class RunDocument
{
    public Guid RunId { get; set; }
    public Guid DocumentId { get; set; }
    public string Role { get; set; } = "offer";
}
