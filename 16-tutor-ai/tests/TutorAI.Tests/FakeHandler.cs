using System.Net;
using System.Text;

namespace TutorAI.Tests;

public sealed class FakeHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond)
    : HttpMessageHandler
{
    public int Calls { get; private set; }
    public List<string> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage r,
        CancellationToken ct
    )
    {
        Bodies.Add(await r.Content!.ReadAsStringAsync(ct));
        return respond(r, ++Calls);
    }

    public static HttpResponseMessage Json(
        string json,
        HttpStatusCode status = HttpStatusCode.OK
    ) => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
