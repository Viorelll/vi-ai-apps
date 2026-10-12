namespace ShopVisionAI.Infrastructure.Http;

public static class ProviderResponseExtensions
{
    public static void EnsureProviderSuccess(this HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;
        var retry = response.Headers.RetryAfter?.Delta;
        if (retry == null && response.Headers.RetryAfter?.Date is { } date)
            retry = date - DateTimeOffset.UtcNow;
        throw new ProviderCallException(response.StatusCode, retry);
    }
}
