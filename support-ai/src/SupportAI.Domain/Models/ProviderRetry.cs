namespace SupportAI.Domain.Models;

public static class ProviderRetry
{
    public static int? Seconds(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        var delay = header?.Delta ?? (header?.Date is { } date ? date - DateTimeOffset.UtcNow : (TimeSpan?)null);
        return delay is null ? null : (int)Math.Clamp(Math.Ceiling(delay.Value.TotalSeconds), 0, 86400);
    }
}
