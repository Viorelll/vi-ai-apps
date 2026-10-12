using System.Net.Http.Headers;
using System.Text.Json;
using SupportAI.Application.Abstractions;
using SupportAI.Domain.DTOs;
using SupportAI.Domain.Models;
namespace SupportAI.Ai.Services;

public sealed class ContentUnderstandingService(HttpClient http, IObjectStore storage, IConfiguration config)
{
    private Uri Endpoint()
    {
        if (!Uri.TryCreate(config["ContentUnderstanding:Endpoint"], UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" || string.IsNullOrWhiteSpace(config["ContentUnderstanding:ApiKey"]) || config["ContentUnderstanding:ApiKey"]!.StartsWith("replace-"))
            throw new SupportException("extraction_not_configured", 503);
        return endpoint;
    }
    public async Task<ExtractionResult> StartAsync(ExtractionRequest input, CancellationToken ct)
    {
        if (!input.ObjectKey.StartsWith("originals/", StringComparison.Ordinal) || input.ObjectKey.Contains(".."))
            throw new SupportException("invalid_object_key");
        var endpoint = Endpoint();
        var analyzer = input.ContentType.StartsWith("audio/") ? "prebuilt-audioSearch" : input.ContentType.StartsWith("video/") ? "prebuilt-videoSearch" : config["ContentUnderstanding:Analyzer"] ?? "prebuilt-documentSearch";
        await using var stream = await storage.OpenAsync(input.ObjectKey, ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, $"contentunderstanding/analyzers/{Uri.EscapeDataString(analyzer)}:analyzeBinary?api-version=2025-11-01"));
        request.Headers.Add("Ocp-Apim-Subscription-Key", config["ContentUnderstanding:ApiKey"]);
        request.Content = new StreamContent(stream);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new SupportException("extraction_request_failed", (int)response.StatusCode, ProviderRetry.Seconds(response));
        if (!response.Headers.TryGetValues("Operation-Location", out var urls))
            throw new SupportException("missing_operation_location", 502);
        var url = urls.Single();
        ValidateOperation(url, endpoint);
        return new("Running", url, []);
    }
    public async Task<ExtractionResult> PollAsync(string url, CancellationToken ct)
    {
        var endpoint = Endpoint();
        ValidateOperation(url, endpoint);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Ocp-Apim-Subscription-Key", config["ContentUnderstanding:ApiKey"]);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new SupportException("extraction_poll_failed", (int)response.StatusCode, ProviderRetry.Seconds(response));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;
        var status = root.GetProperty("status").GetString();
        if (status is "Running" or "NotStarted" or "running" or "notStarted")
            return new("Running", url, []);
        if (!string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase))
            throw new SupportException("extraction_failed", 422);
        var sources = new List<ExtractionSource>();
        foreach (var content in root.GetProperty("result").GetProperty("contents").EnumerateArray())
        {
            var text = content.TryGetProperty("markdown", out var markdown) ? markdown.GetString() : null;
            if (string.IsNullOrWhiteSpace(text))
                continue;
            var page = content.TryGetProperty("startPageNumber", out var p) ? p.GetInt32() : (int?)null;
            var start = content.TryGetProperty("startTimeMs", out var s) ? s.GetInt64() : (long?)null;
            var end = content.TryGetProperty("endTimeMs", out var e) ? e.GetInt64() : (long?)null;
            sources.Add(new(text, page, start, end));
        }
        return new("Succeeded", url, sources, root.GetRawText());
    }
    public static void ValidateOperation(string url, Uri endpoint)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != endpoint.Host || uri.Port != endpoint.Port || !uri.AbsolutePath.StartsWith("/contentunderstanding/", StringComparison.Ordinal))
            throw new SupportException("invalid_operation_url");
    }
}
