using System.Net;
using System.Text;
namespace EvalAI.Tests;

public sealed class RecordingHandler(Func<int, string, HttpResponseMessage> reply) : HttpMessageHandler
{
    public List<string> Requests { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct); Requests.Add(body); return reply(Requests.Count, body); }
    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
