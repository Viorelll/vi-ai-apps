using System.Net;
using System.Text;
namespace SupportAI.UnitTests;

public sealed class QueueHttpHandler(params string[] responses) : HttpMessageHandler
{
    private readonly Queue<string> replies = new(responses);
    public List<string> Requests { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(replies.Dequeue(), Encoding.UTF8, "application/json")
        };
    }
}
