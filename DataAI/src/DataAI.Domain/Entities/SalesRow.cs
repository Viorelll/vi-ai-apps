namespace DataAI.Domain.Entities;

public sealed class SalesRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DatasetId { get; set; }
    public DateOnly SoldOn { get; set; }
    public string Category { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "EUR";
}
