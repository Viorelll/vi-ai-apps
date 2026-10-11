using System.Net.Http.Headers;
using System.Text.Json;
using DocuAI.Ai.Services.Exceptions;
using Microsoft.Extensions.Options;

namespace DocuAI.Ai.Services;

public sealed class ContentUnderstandingService(
    IHttpClientFactory httpClientFactory,
    IOptions<ContentUnderstandingOptions> options)
{
    public async Task<string> ExtractAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Endpoint) || string.IsNullOrWhiteSpace(settings.ApiKey) ||
            settings.ApiKey.StartsWith("replace-", StringComparison.OrdinalIgnoreCase))
        {
            throw new ContentUnderstandingConfigurationException();
        }

        await using var input = file.OpenReadStream();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{settings.Endpoint.TrimEnd('/')}/contentunderstanding/analyzers/" +
            $"{Uri.EscapeDataString(settings.Analyzer)}:analyzeBinary?api-version=2025-11-01");
        request.Headers.Add("Ocp-Apim-Subscription-Key", settings.ApiKey);
        request.Content = new StreamContent(input);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        var http = httpClientFactory.CreateClient("content-understanding");
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && (int)response.StatusCode != 202)
        {
            throw new ContentUnderstandingRequestException();
        }

        if (!response.Headers.TryGetValues("Operation-Location", out var operationLocations))
        {
            throw new ContentUnderstandingRequestException();
        }

        var operationUrl = operationLocations.Single();
        var endpoint = new Uri(settings.Endpoint);
        if (!Uri.TryCreate(operationUrl, UriKind.Absolute, out var operationUri) || operationUri.Host != endpoint.Host)
        {
            throw new ContentUnderstandingRequestException();
        }

        for (var attempt = 0; attempt < 60; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            using var poll = new HttpRequestMessage(HttpMethod.Get, operationUri);
            poll.Headers.Add("Ocp-Apim-Subscription-Key", settings.ApiKey);
            using var result = await http.SendAsync(poll, cancellationToken);
            if (!result.IsSuccessStatusCode)
            {
                throw new ContentUnderstandingRequestException();
            }

            using var payload = await JsonDocument.ParseAsync(
                await result.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            var status = payload.RootElement.TryGetProperty("status", out var statusValue)
                ? statusValue.GetString()
                : null;

            if (status is "Failed" or "Canceled")
            {
                throw new ContentUnderstandingRequestException();
            }

            if (status is "Succeeded" or "Completed")
            {
                return ExtractText(payload.RootElement);
            }
        }

        throw new ContentUnderstandingRequestException();
    }

    private static string ExtractText(JsonElement root)
    {
        var values = new List<string>();
        CollectText(root, values);
        return string.Join("\n\n", values.Distinct());
    }

    private static void CollectText(JsonElement element, ICollection<string> values)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if ((property.Name.Equals("markdown", StringComparison.OrdinalIgnoreCase) ||
                     property.Name.Equals("text", StringComparison.OrdinalIgnoreCase)) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    var text = property.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) values.Add(text);
                }
                else
                {
                    CollectText(property.Value, values);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray()) CollectText(child, values);
        }
    }
}

