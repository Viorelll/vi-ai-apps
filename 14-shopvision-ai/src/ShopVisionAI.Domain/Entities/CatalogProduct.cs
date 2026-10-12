namespace ShopVisionAI.Domain.Entities;

public sealed class CatalogProduct
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string AttributesJson { get; set; } = "{}";
    public string Sheet { get; set; } = "";
    public decimal Price { get; set; }
    public string Currency { get; set; } = "MDL";
    public int Quantity { get; set; }
    public DateTimeOffset CapturedAtUtc { get; set; }
    public string CatalogVersion { get; set; } = "seed-v1";
}
