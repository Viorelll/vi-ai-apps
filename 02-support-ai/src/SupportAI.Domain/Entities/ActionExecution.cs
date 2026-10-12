namespace SupportAI.Domain.Entities;

public sealed class ActionExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProposalId
    {
        get; set;
    }
    public required string IdempotencyKey
    {
        get; set;
    }
    public required string ResultJson
    {
        get; set;
    }
    public DateTimeOffset ExecutedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
