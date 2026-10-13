namespace BuildBudgetAI.Domain.Entities;

public sealed class PlanSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlanId { get; set; }
    public Guid DocumentId { get; set; }
    public string Role { get; set; } = "offer";
}
