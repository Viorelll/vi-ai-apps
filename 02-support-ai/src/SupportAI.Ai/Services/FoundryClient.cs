using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SupportAI.Ai.Models;
using SupportAI.Domain.Models;
namespace SupportAI.Ai.Services;

public sealed class FoundryClient(HttpClient http, IOptions<FoundryOptions> options)
{
    public async Task<JsonDocument> PostAsync(string path, object body, CancellationToken ct)
    {
        var settings = options.Value;
        if (!Uri.TryCreate(settings.OpenAiEndpoint, UriKind.Absolute, out var root) || root.Scheme != "https" || string.IsNullOrWhiteSpace(settings.ApiKey) || settings.ApiKey.StartsWith("replace-"))
            throw new SupportException("foundry_not_configured", 503);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(root, path));
        request.Headers.Add("api-key", settings.ApiKey);
        request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new SupportException(response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ? "provider_rate_limited" : "foundry_request_failed", (int)response.StatusCode, ProviderRetry.Seconds(response));
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }
}
