using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using ProcureAI.Application.Abstractions;
using ProcureAI.Application.Exceptions;
using ProcureAI.Domain.DTOs;

namespace ProcureAI.Ai.Providers;

public sealed class ExtractionProvider(HttpClient http, IConfiguration configuration, IObjectStore objects)
{
    public async Task<ExtractionResult> ExtractAsync(ExtractionRequest request, CancellationToken ct)
    {
        if (!request.ObjectKey.StartsWith($"originals/{request.DocumentId}/", StringComparison.Ordinal))
            throw new AppException("invalid_object_scope");
        if (Path.GetExtension(request.FileName).Equals(".docx", StringComparison.OrdinalIgnoreCase))
        {
            await using var source = await objects.OpenAsync(request.ObjectKey, ct);
            await using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            using var zip = new ZipArchive(buffer, ZipArchiveMode.Read);
            var entry = zip.GetEntry("word/document.xml") ?? throw new AppException("invalid_docx", 422);
            if (entry.Length > 2000000) throw new AppException("docx_limit", 422);
            await using var content = entry.Open();
            using var reader = XmlReader.Create(content, new XmlReaderSettings
            { Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2000000 });
            var xml = await XDocument.LoadAsync(reader, LoadOptions.None, ct);
            XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var text = string.Join("\n", xml.Descendants(word + "p").Select(p => string.Concat(p.Descendants(word + "t").Select(t => t.Value))));
            return new ExtractionResult("completed", null, text);
        }
        var endpoint = configuration["ContentUnderstanding:Endpoint"] ?? "";
        var key = configuration["ContentUnderstanding:ApiKey"] ?? "";
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var root) || root.Scheme != "https" ||
            string.IsNullOrWhiteSpace(key) || key.StartsWith("replace-", StringComparison.OrdinalIgnoreCase) ||
            root.Host.StartsWith("RESOURCE", StringComparison.OrdinalIgnoreCase))
            throw new AppException("content_understanding_not_configured", 503);
        if (request.ProviderOperation is null)
        {
            var analyzer = configuration["ContentUnderstanding:Analyzer"] ?? "prebuilt-documentSearch";
            using var start = new HttpRequestMessage(HttpMethod.Post,
                $"{endpoint.TrimEnd('/')}/contentunderstanding/analyzers/{Uri.EscapeDataString(analyzer)}:analyzeBinary?api-version=2025-11-01");
            start.Headers.Add("Ocp-Apim-Subscription-Key", key);
            start.Content = new StreamContent(await objects.OpenAsync(request.ObjectKey, ct));
            start.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            using var response = await http.SendAsync(start, ct);
            EnsureSuccess(response);
            if (!response.Headers.TryGetValues("Operation-Location", out var values)) throw new AppException("invalid_extraction_response", 422);
            var operation = values.Single();
            ValidateOperation(operation, root);
            return new ExtractionResult("running", operation, null);
        }
        var operationUri = ValidateOperation(request.ProviderOperation, root);
        using var poll = new HttpRequestMessage(HttpMethod.Get, operationUri);
        poll.Headers.Add("Ocp-Apim-Subscription-Key", key);
        using var result = await http.SendAsync(poll, ct);
        EnsureSuccess(result);
        using var payload = await JsonDocument.ParseAsync(await result.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var status = payload.RootElement.GetProperty("status").GetString();
        if (status is "Failed" or "Canceled") throw new AppException("extraction_failed", 422);
        if (status is not ("Succeeded" or "Completed")) return new ExtractionResult("running", request.ProviderOperation, null);
        await using var raw = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(payload.RootElement.GetRawText()));
        await objects.PutAsync($"artifacts/{request.DocumentId}/cu-v1/raw.json", raw, "application/json", ct);
        var texts = new List<string>();
        CollectText(payload.RootElement, texts);
        return new ExtractionResult("completed", request.ProviderOperation, string.Join("\n\n", texts.Distinct()));
    }

    public static Uri ValidateOperation(string operation, Uri root)
    {
        if (!Uri.TryCreate(operation, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != root.Host ||
            uri.Port != root.Port || uri.UserInfo.Length > 0 || !uri.AbsolutePath.StartsWith("/contentunderstanding/", StringComparison.Ordinal))
            throw new AppException("invalid_provider_operation", 422);
        return uri;
    }
    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) throw new AppException("extraction_provider_failed",
            (int)response.StatusCode == 429 ? 429 : (int)response.StatusCode >= 500 ? 503 : 422);
    }
    private static void CollectText(JsonElement element, List<string> texts)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name is "markdown" or "text" && property.Value.ValueKind == JsonValueKind.String)
                { var text = property.Value.GetString(); if (!string.IsNullOrWhiteSpace(text)) texts.Add(text); }
                else CollectText(property.Value, texts);
            }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CollectText(item, texts);
    }
}
