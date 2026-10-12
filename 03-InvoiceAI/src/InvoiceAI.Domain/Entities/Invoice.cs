namespace InvoiceAI.Domain.Entities;

public sealed class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId
    {
        get; set;
    }
    public Document Document { get; set; } = null!;
    public string Kind { get; set; } = "invoice";
    public string? VendorName
    {
        get; set;
    }
    public string? InvoiceNumber
    {
        get; set;
    }
    public DateOnly? Date
    {
        get; set;
    }
    public string? Currency
    {
        get; set;
    }
    public decimal? Subtotal
    {
        get; set;
    }
    public decimal? Tax
    {
        get; set;
    }
    public decimal? Total
    {
        get; set;
    }
    public long Revision
    {
        get; set;
    }
    public string State { get; set; } = "Uploaded";
    public string? Explanation
    {
        get; set;
    }
    public string? SuggestedCategory
    {
        get; set;
    }
    public string? ErrorCode
    {
        get; set;
    }
    public long InputTokens
    {
        get; set;
    }
    public long OutputTokens
    {
        get; set;
    }
    public List<InvoiceLine> Lines { get; set; } = [];
    public List<ExtractedField> Fields { get; set; } = [];
    public List<ValidationIssue> Issues { get; set; } = [];
}
