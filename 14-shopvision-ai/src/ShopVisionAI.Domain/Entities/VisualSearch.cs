namespace ShopVisionAI.Domain.Entities;

public sealed class VisualSearch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ImageDocumentId { get; set; }
    public Document ImageDocument { get; set; } = null!;
    public string? Question { get; set; }
    public string State { get; set; } = "Created";
    public int Revision { get; set; } = 1;
    public string CatalogVersion { get; set; } = "seed-v1";
    public string? ResultJson { get; set; }
    public string? ErrorCode { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
