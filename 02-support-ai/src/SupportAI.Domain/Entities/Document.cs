namespace SupportAI.Domain.Entities;

public sealed class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string FileName
    {
        get; set;
    }
    public required string ContentType
    {
        get; set;
    }
    public required string Sha256
    {
        get; set;
    }
    public required string ObjectKey
    {
        get; set;
    }
    public long SizeBytes
    {
        get; set;
    }
    public string Status { get; set; } = "Uploaded";
    public int ActiveGeneration
    {
        get; set;
    }
    public bool IsPolicy
    {
        get; set;
    }
    public string? ErrorCode
    {
        get; set;
    }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
