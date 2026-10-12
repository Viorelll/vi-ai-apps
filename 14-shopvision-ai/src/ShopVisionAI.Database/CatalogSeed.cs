using ShopVisionAI.Domain.Entities;

namespace ShopVisionAI.Database;

public static class CatalogSeed
{
    public static readonly DateTimeOffset CapturedAt = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    public static readonly CatalogProduct[] Products =
    [
        new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Sku = "USB-BLK-01",
            Name = "Adaptor USB-C Compact",
            Category = "adapter",
            AttributesJson = "{\"color\":\"black\",\"connector\":\"usb\",\"shape\":\"compact\"}",
            Sheet = "Adaptor USB-C negru; USB 3.0 conform fișei seed. Date artificiale.",
            Price = 149.90m,
            Quantity = 8,
            CapturedAtUtc = CapturedAt,
        },
        new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Sku = "USB-BLK-02",
            Name = "Adaptor USB-C Travel",
            Category = "adapter",
            AttributesJson = "{\"color\":\"black\",\"connector\":\"usb\",\"shape\":\"compact\"}",
            Sheet = "Adaptor USB-C negru; USB 2.0 conform fișei seed. Date artificiale.",
            Price = 99.50m,
            Quantity = 0,
            CapturedAtUtc = CapturedAt,
        },
        new()
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Sku = "MOUSE-WHT-01",
            Name = "Mouse alb Wireless",
            Category = "mouse",
            AttributesJson = "{\"color\":\"white\",\"shape\":\"rounded\"}",
            Sheet = "Mouse alb wireless. Date artificiale.",
            Price = 249m,
            Quantity = 4,
            CapturedAtUtc = CapturedAt,
        },
    ];
}
