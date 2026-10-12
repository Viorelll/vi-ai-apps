namespace ShopVisionAI.Domain.Entities;

public sealed class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sha256 { get; set; } = "";
    public string ObjectKey { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Mime { get; set; } = "";
    public long Size { get; set; }
    public string Status { get; set; } = "Ready";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
