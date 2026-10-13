namespace BuildBudgetAI.Domain.Entities;

public sealed class BudgetSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlanId { get; set; }
    public long Revision { get; set; }
    public string InputsHash { get; set; } = "";
    public string LinesJson { get; set; } = "[]";
    public decimal Total { get; set; }
    public string Currency { get; set; } = "EUR";
    public bool Incomplete { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
