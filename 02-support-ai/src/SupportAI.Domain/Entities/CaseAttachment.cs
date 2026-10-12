namespace SupportAI.Domain.Entities;

public sealed class CaseAttachment
{
    public Guid CaseId
    {
        get; set;
    }
    public Guid DocumentId
    {
        get; set;
    }
    public string Kind { get; set; } = "Evidence";
}
