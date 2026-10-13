namespace TutorAI.Domain.Entities;

public sealed class AdaptationStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid AttemptId { get; set; }
    public int PreviousLevel { get; set; }
    public int NextLevel { get; set; }
    public string RuleVersion { get; set; } = "last-two-v1";
}
