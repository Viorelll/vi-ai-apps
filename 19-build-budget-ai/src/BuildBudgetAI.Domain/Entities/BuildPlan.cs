namespace BuildBudgetAI.Domain.Entities;

public sealed class BuildPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Currency { get; set; } = "EUR";
    public string State { get; set; } = "Created";
    public long Revision { get; set; }
    public List<MaterialNeed> Needs { get; set; } = [];
    public List<MaterialOffer> Offers { get; set; } = [];
    public List<PlanSource> Sources { get; set; } = [];
}
