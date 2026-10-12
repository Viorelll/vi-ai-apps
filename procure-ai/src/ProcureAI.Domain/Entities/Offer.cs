namespace ProcureAI.Domain.Entities;

public sealed class Offer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public Guid DocumentId { get; set; }
    public required string SupplierLabel { get; set; }
    public string? Currency { get; set; }
    public string? Unit { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Net { get; set; }
    public decimal? Shipping { get; set; }
    public int? LeadDays { get; set; }
    public bool? MeetsRequirements { get; set; }
    public required string EvidenceJson { get; set; }
}
