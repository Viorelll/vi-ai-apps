namespace ImageAI.Domain.Entities;

public sealed class ImageRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid RequestId { get; set; }
    public string RequestHash { get; set; } = "";
    public string Kind { get; set; } = "Generate";
    public string Prompt { get; set; } = "";
    public string Size { get; set; } = "1024x1024";
    public string Quality { get; set; } = "medium";
    public Guid? InputAssetId { get; set; }
    public Guid? InputDocumentId { get; set; }
    public Guid? MaskDocumentId { get; set; }
    public string State { get; set; } = "Queued";
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
