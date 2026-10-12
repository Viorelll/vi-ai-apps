using System.Net;
using System.Text;
namespace InvoiceAI.Tests.Fixtures;

public sealed class FakeHttpHandler(Func<int, string, string> response) : HttpMessageHandler
{
    public int Calls
    {
        get; private set;
    }
    public List<string> Payloads { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = await request.Content!.ReadAsStringAsync(ct);
        Payloads.Add(body);
        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(response(Calls++, body), Encoding.UTF8, "application/json")
        };
    }
}
