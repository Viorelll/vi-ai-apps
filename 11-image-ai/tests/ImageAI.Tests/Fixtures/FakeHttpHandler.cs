namespace ImageAI.Tests.Fixtures;

public sealed class FakeHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct
    ) => response(request);
}
