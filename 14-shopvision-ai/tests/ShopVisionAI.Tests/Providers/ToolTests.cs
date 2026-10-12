using Microsoft.Extensions.Options;
using ShopVisionAI.Ai.Providers;
using ShopVisionAI.Ai.Tools;
using ShopVisionAI.Contracts.Dto;
using ShopVisionAI.Contracts.Validation;

namespace ShopVisionAI.Tests.Providers;

public sealed class ToolTests
{
    private static readonly Candidate Seed = new(
        "USB-BLK-01",
        "USB",
        "adapter",
        .8,
        "Candidat.",
        "149.90",
        "MDL",
        8,
        "stale",
        DateTimeOffset.UtcNow,
        ["catalog:USB-BLK-01", "stock:USB-BLK-01"],
        "/api/visual/catalog/USB-BLK-01"
    );

    [Fact]
    public void ToolRejectsUnknownSku() =>
        Assert.Throws<ArgumentException>(() =>
            new CatalogSnapshotTool().Execute(
                "get_catalog_snapshot",
                "{\"sku\":\"not-allowed\"}",
                [Seed]
            )
        );

    [Fact]
    public void ToolRejectsWrites() =>
        Assert.Throws<ArgumentException>(() =>
            new CatalogSnapshotTool().Execute("purchase", "{}", [Seed])
        );

    [Fact]
    public void InventedGroundingRejected() =>
        Assert.Throws<ArgumentException>(() =>
            GroundingValidator.Validate(
                new("answer", [new(Seed.Sku, "reason", ["fake"])], ["fake"], 1, 1, false),
                [Seed]
            )
        );

    [Fact]
    public async Task FoundryToolRoundTripsReadOnlySnapshot()
    {
        var handler = new ToolHandler([Seed]);
        var provider = new FoundryGroundedProvider(
            new HttpClient(handler),
            Options.Create(new FoundryOptions { Endpoint = "https://example.invalid/openai/v1/" }),
            new CatalogSnapshotTool()
        );
        var response = await provider.GenerateAsync(
            new(Guid.NewGuid(), "Ce stoc?", [], [Seed]),
            default
        );
        Assert.Equal(2, handler.Calls);
        Assert.True(handler.ReceivedToolResult);
        Assert.Equal(40, response.InputTokens);
        Assert.Equal(20, response.OutputTokens);
    }

    [Fact]
    public async Task ToolLoopHasHardLimit()
    {
        var handler = new ToolHandler([Seed], true);
        var provider = new FoundryGroundedProvider(
            new HttpClient(handler),
            Options.Create(new FoundryOptions { Endpoint = "https://example.invalid/openai/v1/" }),
            new CatalogSnapshotTool()
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.GenerateAsync(new(Guid.NewGuid(), "Ce stoc?", [], [Seed]), default)
        );
        Assert.Equal(5, handler.Calls);
    }
}
