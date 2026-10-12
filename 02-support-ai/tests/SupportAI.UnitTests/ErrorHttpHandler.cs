using System.Net;
using System.Text;
namespace SupportAI.UnitTests;

public sealed class ErrorHttpHandler(string code) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
    {
        Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { code, retryAfterSeconds = (int?)null }), Encoding.UTF8, "application/problem+json")
    });
}
