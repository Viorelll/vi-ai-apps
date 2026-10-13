namespace VideoSearchAI.Tests;

public sealed class FakeHttpHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond)
    : HttpMessageHandler
{
    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct
    ) => Task.FromResult(respond(request, ++Calls));

    public static HttpResponseMessage Json(
        string body,
        System.Net.HttpStatusCode status = System.Net.HttpStatusCode.OK
    ) =>
        new(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
}
