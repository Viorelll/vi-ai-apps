using System.Net.Http.Headers;
using System.Text.Json;
using DocuAI.Application.Abstractions;
using DocuAI.Application.Exceptions;
using Microsoft.Extensions.Configuration;

namespace DocuAI.Infrastructure.Ai;

public sealed class HttpAiExtractionClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : IAiExtractionClient
{
    public async Task<string> ExtractAsync(string fileName, Stream content, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/extractions") { Content = form };
        request.Headers.Add("X-Internal-Key", configuration["Internal:Key"] ?? "local-placeholder");
        using var response = await httpClientFactory.CreateClient("ai-gateway").SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AiExtractionException();
        }

        using var payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        return payload.RootElement.GetProperty("text").GetString() ?? string.Empty;
    }
}

