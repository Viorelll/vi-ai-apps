namespace ModerateAI.Domain.Entities;

public sealed class PolicyDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItemId { get; set; }
    public required string PolicyVersion { get; set; }
    public required string Decision { get; set; }
    public required string ReasonsJson { get; set; }
    public required string DecisionHash { get; set; }
}
