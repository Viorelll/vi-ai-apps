namespace InvoiceAI.Domain.Entities;

public sealed class InvoiceLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId
    {
        get; set;
    }
    public int Ordinal
    {
        get; set;
    }
    public string? Description
    {
        get; set;
    }
    public decimal? Quantity
    {
        get; set;
    }
    public decimal? UnitPrice
    {
        get; set;
    }
    public decimal? Amount
    {
        get; set;
    }
    public string? Currency
    {
        get; set;
    }
    public int? PageNumber
    {
        get; set;
    }
}
