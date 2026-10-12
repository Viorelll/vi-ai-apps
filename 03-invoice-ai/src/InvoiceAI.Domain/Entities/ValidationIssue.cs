namespace InvoiceAI.Domain.Entities;

public sealed class ValidationIssue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId
    {
        get; set;
    }
    public string Code { get; set; } = "";
    public string Severity { get; set; } = "error";
    public string Path { get; set; } = "";
    public string DetailsJson { get; set; } = "{}";
}
