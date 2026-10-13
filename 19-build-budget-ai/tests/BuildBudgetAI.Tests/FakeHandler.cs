namespace BuildBudgetAI.Tests;

public sealed class FakeHandler(Func<HttpRequestMessage, int, HttpResponseMessage> response)
    : HttpMessageHandler
{
    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct
    ) => Task.FromResult(response(request, Calls++));
}
