namespace InvoiceAI.Domain.Entities;

public sealed class ExportSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId
    {
        get; set;
    }
    public long Revision
    {
        get; set;
    }
    public string SchemaVersion { get; set; } = "1";
    public string PayloadJson { get; set; } = "{}";
    public string Hash { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
