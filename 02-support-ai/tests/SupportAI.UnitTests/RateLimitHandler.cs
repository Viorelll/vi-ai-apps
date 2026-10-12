using System.Net;
using System.Net.Http.Headers;
namespace SupportAI.UnitTests;

public sealed class RateLimitHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(45));
        return Task.FromResult(response);
    }
}
