using System.Net;
using System.Net.Http.Headers;

namespace InspectAI.Tests.Fakes;

public sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct
    )
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent("{}") };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(17));
        return Task.FromResult(response);
    }
}
