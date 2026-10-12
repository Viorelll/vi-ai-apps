namespace ImageAI.Domain.Entities;

public sealed class ImageAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid RunId { get; set; }
    public Guid? ParentAssetId { get; set; }
    public string ObjectKey { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
