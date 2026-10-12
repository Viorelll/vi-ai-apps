namespace InvoiceAI.Domain.Entities;

public sealed class ExtractedField
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId
    {
        get; set;
    }
    public string Path { get; set; } = "";
    public string RawText { get; set; } = "";
    public string ValueJson { get; set; } = "null";
    public decimal? Confidence
    {
        get; set;
    }
    public string EvidenceJson { get; set; } = "[]";
    public string? CorrectedValueJson
    {
        get; set;
    }
    public DateTimeOffset? CorrectedAtUtc
    {
        get; set;
    }
}
