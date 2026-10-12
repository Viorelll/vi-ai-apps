namespace InspectAI.Domain.Entities;

public sealed class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string ObjectKey { get; set; } = "";
    public string? PhotoObjectKey { get; set; }
    public long SizeBytes { get; set; }
    public string Status { get; set; } = "Uploaded";
    public string? ErrorCode { get; set; }
    public int ActiveGeneration { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
