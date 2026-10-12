using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Xml.Linq;
using ContractAI.Domain.DTOs;
using ContractAI.Domain.Models;
using ContractAI.Infrastructure.Abstractions;
using Microsoft.Extensions.Configuration;

namespace ContractAI.Infrastructure.Extraction;

public sealed class DocumentExtractor(HttpClient http, IObjectStore storage, IConfiguration config)
{
    public async Task<ExtractionResult> StartAsync(ExtractionStart input, CancellationToken ct)
    {
        await using var stream = await storage.OpenReadAsync(input.ObjectKey, ct);
        if (input.ContentType == "text/plain")
        {
            using var reader = new StreamReader(stream, new System.Text.UTF8Encoding(false, true));
            var text = await reader.ReadToEndAsync(ct);
            return new("Succeeded", null, [new(null, text)]);
        }
        if (
            input.ContentType
            == "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        )
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var entry =
                archive.GetEntry("word/document.xml")
                ?? throw new AppException(422, "invalid_docx", "DOCX document.xml is missing.");
            if (entry.Length > 10 * 1024 * 1024)
                throw new AppException(
                    422,
                    "docx_expansion_limit",
                    "DOCX expanded text exceeds limit."
                );
            await using var xmlStream = entry.Open();
            using var xmlReader = System.Xml.XmlReader.Create(
                xmlStream,
                new System.Xml.XmlReaderSettings
                {
                    Async = true,
                    DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                    MaxCharactersInDocument = 10 * 1024 * 1024,
                }
            );
            var xml = await XDocument.LoadAsync(xmlReader, LoadOptions.None, ct);
            XNamespace ns = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            return new(
                "Succeeded",
                null,
                [
                    new(
                        null,
                        string.Join(
                            "\n\n",
                            xml.Descendants(ns + "p")
                                .Select(p =>
                                    string.Concat(p.Descendants(ns + "t").Select(t => t.Value))
                                )
                        )
                    ),
                ]
            );
        }
        var endpoint = Endpoint();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(
                endpoint,
                "contentunderstanding/analyzers/"
                    + Uri.EscapeDataString(
                        config["ContentUnderstanding:Analyzer"] ?? "prebuilt-documentSearch"
                    )
                    + ":analyzeBinary?api-version=2025-11-01"
            )
        );
        request.Headers.Add("Ocp-Apim-Subscription-Key", config["ContentUnderstanding:ApiKey"]);
        request.Content = new StreamContent(stream);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await http.SendAsync(request, ct);
        CheckResponse(response);
        if (!response.Headers.TryGetValues("Operation-Location", out var locations))
            throw new AppException(
                502,
                "missing_operation_url",
                "Extractor omitted operation URL."
            );
        var url = locations.Single();
        ValidateOperationUrl(url, endpoint);
        return new("Running", url);
    }

    public async Task<ExtractionResult> PollAsync(ExtractionPoll input, CancellationToken ct)
    {
        var endpoint = Endpoint();
        ValidateOperationUrl(input.OperationUrl, endpoint);
        using var request = new HttpRequestMessage(HttpMethod.Get, input.OperationUrl);
        request.Headers.Add("Ocp-Apim-Subscription-Key", config["ContentUnderstanding:ApiKey"]);
        using var response = await http.SendAsync(request, ct);
        CheckResponse(response);
        var raw = await response.Content.ReadAsStringAsync(ct);
        using var json = JsonDocument.Parse(raw);
        var root = json.RootElement;
        var status = root.GetProperty("status").GetString();
        if (status is "Failed" or "Canceled" or "failed" or "canceled")
            return new("Failed", input.OperationUrl, ErrorCode: "extraction_provider_failed");
        if (status is not ("Succeeded" or "succeeded"))
            return new("Running", input.OperationUrl);
        var pages = ParsePages(root);
        return new("Succeeded", input.OperationUrl, pages, RawJson: raw);
    }

    public static ExtractedPage[] ParsePages(JsonElement root)
    {
        var pages = new List<ExtractedPage>();
        foreach (var content in root.GetProperty("result").GetProperty("contents").EnumerateArray())
        {
            var startCount = pages.Count;
            // CU page lines preserve page provenance; use markdown only when no positioned text exists.
            if (content.TryGetProperty("pages", out var sourcePages))
                foreach (var page in sourcePages.EnumerateArray())
                {
                    var text = page.TryGetProperty("lines", out var lines)
                        ? string.Join(
                            "\n",
                            lines
                                .EnumerateArray()
                                .Where(x => x.TryGetProperty("content", out _))
                                .Select(x => x.GetProperty("content").GetString())
                        )
                        : "";
                    if (!string.IsNullOrWhiteSpace(text))
                        pages.Add(
                            new(page.GetProperty("pageNumber").GetInt32(), text, page.GetRawText())
                        );
                }
            if (
                pages.Count == startCount
                && content.TryGetProperty("markdown", out var markdown)
                && !string.IsNullOrWhiteSpace(markdown.GetString())
            )
                pages.Add(new(null, markdown.GetString()!)); // Unknown page stays unknown; never fabricate page 1.
        }
        if (pages.Count == 0)
            throw new AppException(422, "empty_extraction", "Extractor returned no text.");
        return pages.ToArray();
    }

    public static void ValidateOperationUrl(string value, Uri endpoint)
    {
        if (
            !Uri.TryCreate(value, UriKind.Absolute, out var url)
            || url.Scheme != "https"
            || url.Host != endpoint.Host
            || url.Port != endpoint.Port
            || !string.IsNullOrEmpty(url.UserInfo)
            || !url.AbsolutePath.StartsWith("/contentunderstanding/", StringComparison.Ordinal)
        )
            throw new AppException(
                502,
                "invalid_operation_url",
                "Untrusted extractor operation URL."
            );
    }

    private Uri Endpoint()
    {
        var key = config["ContentUnderstanding:ApiKey"];
        if (
            string.IsNullOrWhiteSpace(key)
            || key.StartsWith("replace-", StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate(
                config["ContentUnderstanding:Endpoint"],
                UriKind.Absolute,
                out var endpoint
            )
            || endpoint.Scheme != "https"
        )
            throw new AppException(
                503,
                "extractor_not_configured",
                "Configure Content Understanding for PDFs and scanned images."
            );
        return new Uri(endpoint.ToString().TrimEnd('/') + "/");
    }

    private static void CheckResponse(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new AppException(
                (int)response.StatusCode is 429 or >= 500 ? 503 : 502,
                "extractor_http_" + (int)response.StatusCode,
                "Content Understanding rejected the request.",
                response.Headers.RetryAfter?.Delta is { } delay
                    ? (int)Math.Clamp(delay.TotalSeconds, 0, 3600)
                    : null
            );
    }
}
