namespace ShopVisionAI.Domain.Rules;

public static class AvailabilityRules
{
    public static string Label(
        int quantity,
        DateTimeOffset capturedAt,
        DateTimeOffset now,
        TimeSpan maxAge
    )
    {
        if (quantity < 0 || capturedAt > now)
            throw new ArgumentException("invalid_stock_snapshot");
        return now - capturedAt > maxAge ? "stale"
            : quantity > 0 ? "in_stock_snapshot"
            : "out_of_stock_snapshot";
    }
}
