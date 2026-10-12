using ShopVisionAI.Domain.Rules;

namespace ShopVisionAI.Tests.Rules;

public sealed class AvailabilityTests
{
    private readonly DateTimeOffset now = new(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(8, "in_stock_snapshot")]
    [InlineData(0, "out_of_stock_snapshot")]
    public void FreshStock(int quantity, string expected) =>
        Assert.Equal(expected, AvailabilityRules.Label(quantity, now, now, TimeSpan.FromHours(24)));

    [Fact]
    public void OldStockIsStale() =>
        Assert.Equal(
            "stale",
            AvailabilityRules.Label(8, now.AddDays(-2), now, TimeSpan.FromHours(24))
        );

    [Fact]
    public void FutureStockRejected() =>
        Assert.Throws<ArgumentException>(() =>
            AvailabilityRules.Label(8, now.AddSeconds(1), now, TimeSpan.FromHours(24))
        );

    [Fact]
    public void NegativeStockRejected() =>
        Assert.Throws<ArgumentException>(() =>
            AvailabilityRules.Label(-1, now, now, TimeSpan.FromHours(24))
        );
}
