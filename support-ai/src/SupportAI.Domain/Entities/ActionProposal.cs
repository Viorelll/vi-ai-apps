namespace SupportAI.Domain.Entities;

public sealed class ActionProposal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaseId
    {
        get; set;
    }
    public Guid MessageId
    {
        get; set;
    }
    public required string Kind
    {
        get; set;
    }
    public required string ArgumentsJson
    {
        get; set;
    }
    public required string PayloadHash
    {
        get; set;
    }
    public string State { get; set; } = "Draft";
    public long Revision
    {
        get; set;
    }
    public DateTimeOffset ExpiresAtUtc { get; set; } = DateTimeOffset.UtcNow.AddMinutes(30);
}
