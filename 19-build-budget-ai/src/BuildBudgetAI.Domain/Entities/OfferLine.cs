namespace BuildBudgetAI.Domain.Entities;

public sealed class OfferLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfferId { get; set; }
    public string MaterialLabel { get; set; } = "";
    public string Unit { get; set; } = "m²";
    public decimal? PackQuantity { get; set; }
    public decimal Price { get; set; }
    public decimal? Transport { get; set; }
    public bool Confirmed { get; set; }
    public string EvidenceJson { get; set; } = "[]";
}
