namespace BuildBudgetAI.Domain.Entities;

public sealed class MaterialOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlanId { get; set; }
    public Guid? DocumentId { get; set; }
    public string SupplierLabel { get; set; } = "";
    public string Currency { get; set; } = "EUR";
    public string TaxBasis { get; set; } = "Unknown";
    public List<OfferLine> Lines { get; set; } = [];
}
