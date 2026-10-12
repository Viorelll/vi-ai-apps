using ShopVisionAI.Contracts.Dto;
using ShopVisionAI.Database;
using ShopVisionAI.Services.Matching;

namespace ShopVisionAI.Tests.Matching;

public sealed class MatchingTests
{
    [Fact]
    public void UsbFixtureReturnsTwoCandidates()
    {
        var result = new MatchingService().Rank(
            [
                new("image:seed", "visible", "category", "adapter", null),
                new("image:seed", "visible", "color", "black", null),
            ],
            CatalogSeed.Products,
            CatalogSeed.CapturedAt.AddHours(1)
        );
        Assert.Equal(new[] { "USB-BLK-01", "USB-BLK-02" }, result.Select(x => x.Sku));
        Assert.Equal("149.90", result[0].Price);
        Assert.Equal(8, result[0].Quantity);
        Assert.Equal(0, result[1].Quantity);
    }

    [Fact]
    public void InferredAttributesDoNotRank() =>
        Assert.Empty(
            new MatchingService().Rank(
                [new("image:seed", "inferred", "category", "adapter", null)],
                CatalogSeed.Products,
                CatalogSeed.CapturedAt
            )
        );

    [Fact]
    public void UnknownCategoryDoesNotFabricateMatches() =>
        Assert.Empty(
            new MatchingService().Rank(
                [new("image:seed", "visible", "category", "unknown", null)],
                CatalogSeed.Products,
                CatalogSeed.CapturedAt
            )
        );
}
