using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AccessReadAI.Ai.Models;
using AccessReadAI.Contracts;
using AccessReadAI.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace AccessReadAI.Ai.Providers;

public sealed class LayoutExtractor(
    HttpClient http,
    IObjectStore storage,
    IOptions<ProviderOptions> options
)
{
    private readonly ProviderOptions config = options.Value;

    public async Task<ExtractionResult> ExtractAsync(
        ExtractionRequest request,
        CancellationToken ct
    )
    {
        if (request.ContentType == "text/plain")
        {
            using var obj = await storage.OpenAsync(request.ObjectKey, ct);
            using var reader = new StreamReader(obj.ResponseStream, new UTF8Encoding(false, true));
            var text = await reader.ReadToEndAsync(ct);
            var blocks = text.Replace("\r", "")
                .Split('\f')
                .SelectMany(
                    (page, p) =>
                        SplitText(page)
                            .Select(
                                (s, i) =>
                                    new ExtractedBlock(
                                        $"page-{p + 1}-b{i + 1}",
                                        0,
                                        p + 1,
                                        "text",
                                        s,
                                        "{}"
                                    )
                            )
                )
                .Select((b, i) => b with { Ordinal = i })
                .ToList();
            if (blocks.Count > 100)
                throw new ProviderFault("document_block_limit");
            return new("Ready", null, blocks, "local-utf8");
        }
        if (config.Demo)
            throw new ProviderFault("scan_requires_azure_layout");
        if (request.OperationUrl == null)
        {
            using var obj = await storage.OpenAsync(request.ObjectKey, ct);
            using var message = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(
                    new Uri(config.LayoutEndpoint),
                    "documentintelligence/documentModels/prebuilt-layout:analyze?api-version=2024-11-30&output=figures"
                )
            );
            message.Headers.Add("Ocp-Apim-Subscription-Key", config.LayoutKey);
            message.Content = new StreamContent(obj.ResponseStream);
            message.Content.Headers.ContentType = new MediaTypeHeaderValue(
                "application/octet-stream"
            );
            using var response = await http.SendAsync(message, ct);
            await ProviderHttp.EnsureAsync(response, ct);
            var operation = response.Headers.GetValues("Operation-Location").Single();
            _ = ProviderHttp.OperationUri(config.LayoutEndpoint, operation);
            return new("Running", operation, [], "azure-layout-2024-11-30");
        }
        var uri = ProviderHttp.OperationUri(config.LayoutEndpoint, request.OperationUrl);
        using var poll = new HttpRequestMessage(HttpMethod.Get, uri);
        poll.Headers.Add("Ocp-Apim-Subscription-Key", config.LayoutKey);
        using var polled = await http.SendAsync(poll, ct);
        using var result = await ProviderHttp.ReadAsync(polled, ct);
        var status = result.RootElement.GetProperty("status").GetString();
        if (status is "running" or "notStarted")
            return new("Running", request.OperationUrl, [], "azure-layout-2024-11-30");
        if (status != "succeeded")
            throw new ProviderFault("layout_extraction_failed");
        var analyzed = result.RootElement.GetProperty("analyzeResult");
        var output = new List<ExtractedBlock>();
        var uncertainPages = new HashSet<int>();
        if (analyzed.TryGetProperty("pages", out var pages))
            foreach (var page in pages.EnumerateArray())
                if (
                    page.TryGetProperty("words", out var words)
                    && words
                        .EnumerateArray()
                        .Any(x => x.TryGetProperty("confidence", out var c) && c.GetDouble() < 0.8)
                )
                    uncertainPages.Add(page.GetProperty("pageNumber").GetInt32());
        if (analyzed.TryGetProperty("paragraphs", out var paragraphs))
            foreach (var p in paragraphs.EnumerateArray())
            {
                var regions = p.GetProperty("boundingRegions");
                var page = regions[0].GetProperty("pageNumber").GetInt32();
                foreach (var text in SplitText(p.GetProperty("content").GetString() ?? ""))
                    output.Add(
                        new(
                            $"page-{page}-b{output.Count + 1}",
                            output.Count,
                            page,
                            p.TryGetProperty("role", out var role) && role.GetString() == "title"
                                ? "heading"
                                : "text",
                            text,
                            regions.GetRawText(),
                            uncertainPages.Contains(page)
                        )
                    );
            }
        if (analyzed.TryGetProperty("figures", out var figures))
            foreach (var figure in figures.EnumerateArray())
            {
                var id = figure.GetProperty("id").GetString()!;
                var regions = figure.GetProperty("boundingRegions");
                var page = regions[0].GetProperty("pageNumber").GetInt32();
                var builder = new UriBuilder(uri);
                builder.Path = builder.Path.TrimEnd('/') + "/figures/" + Uri.EscapeDataString(id);
                using var crop = new HttpRequestMessage(HttpMethod.Get, builder.Uri);
                crop.Headers.Add("Ocp-Apim-Subscription-Key", config.LayoutKey);
                using var cropResponse = await http.SendAsync(crop, ct);
                await ProviderHttp.EnsureAsync(cropResponse, ct);
                var image = await cropResponse.Content.ReadAsByteArrayAsync(ct);
                if (image.Length > 5_000_000)
                    throw new ProviderFault("figure_size_limit");
                var key = $"figures/{request.DocumentId}/{Uri.EscapeDataString(id)}.png";
                await storage.PutBytesAsync(key, image, "image/png", ct);
                var caption = figure.TryGetProperty("caption", out var cap)
                    ? cap.GetProperty("content").GetString() ?? ""
                    : "";
                output.Add(
                    new(
                        $"page-{page}-img-{id}",
                        output.Count,
                        page,
                        "image",
                        caption,
                        regions.GetRawText(),
                        false,
                        key
                    )
                );
            }
        if (output.Count > 100)
            throw new ProviderFault("document_block_limit");
        return new(
            "Ready",
            null,
            output.OrderBy(x => x.PageNumber).Select((b, i) => b with { Ordinal = i }).ToList(),
            "azure-layout-2024-11-30"
        );
    }

    public static IEnumerable<string> SplitText(string text)
    {
        foreach (var paragraph in text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = paragraph.Trim();
            while (normalized.Length > 0)
            {
                var take = Math.Min(2500, normalized.Length);
                if (take < normalized.Length)
                {
                    var boundary = normalized.LastIndexOf(' ', take - 1, take);
                    if (boundary > 100)
                        take = boundary;
                }
                yield return normalized[..take];
                normalized = normalized[take..].Trim();
            }
        }
    }
}
