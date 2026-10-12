using System.Net;

namespace ShopVisionAI.Infrastructure.Http;

public sealed class ProviderCallException(HttpStatusCode status, TimeSpan? retryAfter)
    : HttpRequestException("provider_request_failed", null, status)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
