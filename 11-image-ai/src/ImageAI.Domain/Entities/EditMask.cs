namespace ImageAI.Domain.Entities;

public sealed class EditMask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Guid InputAssetId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string RegionHash { get; set; } = "";
}
