using System.Net;
using System.Text;

namespace InspectAI.Tests.Fakes;

public sealed class StubHandler(Func<HttpRequestMessage, int, Task<string>> respond)
    : HttpMessageHandler
{
    public int Count { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct
    ) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                await respond(request, Count++),
                Encoding.UTF8,
                "application/json"
            ),
        };
}
