namespace BuildBudgetAI.Domain.Entities;

public sealed class MaterialNeed
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlanId { get; set; }
    public string Label { get; set; } = "";
    public string Unit { get; set; } = "m²";
    public decimal Quantity { get; set; }
    public decimal WastePercent { get; set; }
}
