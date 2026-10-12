namespace ProcureAI.Tests.Unit;

public sealed class StubHttpHandler(Func<int, HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    private int calls;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        Task.FromResult(respond(++calls, request));
}
