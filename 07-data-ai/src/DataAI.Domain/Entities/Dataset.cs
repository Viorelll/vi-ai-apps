namespace DataAI.Domain.Entities;

public sealed class Dataset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string SchemaVersion { get; set; } = "sales-v1";
    public string? ObjectKey { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<SalesRow> Rows { get; set; } = [];
}
