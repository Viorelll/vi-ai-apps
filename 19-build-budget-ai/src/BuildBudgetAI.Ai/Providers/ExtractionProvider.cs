using System.Text;
using System.Text.Json;
using BuildBudgetAI.Ai.Models;
using BuildBudgetAI.Domain.Dto;
using BuildBudgetAI.Domain.Interfaces;
using BuildBudgetAI.Domain.Models;

namespace BuildBudgetAI.Ai.Providers;

public sealed class ExtractionProvider(
    HttpClient http,
    IObjectStore store,
    FoundryOptions foundry,
    ExtractionOptions options
)
{
    public async Task<ExtractionResult> ExtractAsync(ExtractionRequest input, CancellationToken ct)
    {
        if (!input.ObjectKey.StartsWith("originals/", StringComparison.Ordinal))
            throw new DomainException("invalid_object_key");
        if (input.ContentType == "text/plain")
        {
            var bytes = await store.ReadAsync(input.ObjectKey, ct);
            string text = new UTF8Encoding(false, true).GetString(bytes);
            if (text.Length > 2000000)
                throw new DomainException("text_limit", 413);
            var sources = new List<EvidenceDto>();
            for (int i = 0; i < text.Length; i += 12000)
                sources.Add(
                    new(
                        $"{input.DocumentId}:{i / 12000}",
                        input.DocumentId,
                        null,
                        text.Substring(i, Math.Min(12000, text.Length - i))
                    )
                );
            return new("Completed", null, sources);
        }
        if (foundry.Mode == "Demo")
            throw new DomainException("azure_extractor_required_for_pdf_and_images", 422);
        var endpoint = new Uri(options.Endpoint);
        bool start = input.OperationUrl == null;
        Uri url = start
            ? new Uri(
                endpoint,
                $"/contentunderstanding/analyzers/{Uri.EscapeDataString(options.AnalyzerId)}:analyzeBinary?api-version=2025-11-01"
            )
            : new Uri(input.OperationUrl!);
        if (
            url.Scheme != "https"
            || url.Host != endpoint.Host
            || url.Port != endpoint.Port
            || !url.AbsolutePath.StartsWith("/contentunderstanding/", StringComparison.Ordinal)
        )
            throw new DomainException("invalid_operation_url");
        using var request = new HttpRequestMessage(start ? HttpMethod.Post : HttpMethod.Get, url);
        request.Headers.Add("Ocp-Apim-Subscription-Key", options.ApiKey);
        if (start)
        {
            request.Content = new ByteArrayContent(await store.ReadAsync(input.ObjectKey, ct));
            request.Content.Headers.ContentType = new("application/octet-stream");
        }
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new DomainException(
                $"extractor_http_{(int)response.StatusCode}",
                (int)response.StatusCode >= 500 ? 503 : 422
            );
        if (start)
        {
            if (!response.Headers.TryGetValues("Operation-Location", out var locations))
                throw new DomainException("missing_operation_location", 422);
            var op = new Uri(locations.Single());
            if (op.Scheme != "https" || op.Host != endpoint.Host || op.Port != endpoint.Port)
                throw new DomainException("invalid_operation_url");
            return new("Running", op.ToString(), []);
        }
        string raw = await response.Content.ReadAsStringAsync(ct);
        using var result = JsonDocument.Parse(raw);
        string? status = result.RootElement.GetProperty("status").GetString();
        if (status is "Running" or "NotStarted" or "running" or "notStarted")
            return new("Running", input.OperationUrl, []);
        if (status is not ("Succeeded" or "succeeded"))
            throw new DomainException("extraction_failed", 422);
        using var rawStream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        await store.PutAsync(
            $"artifacts/{input.DocumentId}/domain-v1/raw.json",
            rawStream,
            "application/json",
            ct
        );
        var evidence = new List<EvidenceDto>();
        foreach (
            var content in result
                .RootElement.GetProperty("result")
                .GetProperty("contents")
                .EnumerateArray()
        )
        {
            if (
                content.TryGetProperty("markdown", out var md)
                && !string.IsNullOrWhiteSpace(md.GetString())
            )
            {
                string text = md.GetString()!;
                int? page = content.TryGetProperty("startPageNumber", out var pn)
                    ? pn.GetInt32()
                    : null;
                for (int i = 0; i < text.Length; i += 12000)
                    evidence.Add(
                        new(
                            $"{input.DocumentId}:{evidence.Count}",
                            input.DocumentId,
                            page,
                            text.Substring(i, Math.Min(12000, text.Length - i))
                        )
                    );
            }
        }
        return new("Completed", input.OperationUrl, evidence);
    }
}
