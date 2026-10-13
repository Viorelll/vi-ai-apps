using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using VideoSearchAI.Ai.Options;
using VideoSearchAI.Contracts.Dto;
using VideoSearchAI.Infrastructure.Storage;

namespace VideoSearchAI.Ai.Providers.Extraction;

public sealed class ContentUnderstandingClient(
    HttpClient http,
    ObjectStore store,
    IOptions<AiOptions> options,
    VideoUnderstandingMapper mapper
)
{
    private readonly AiOptions o = options.Value;

    public async Task<ExtractionResult> StartAsync(ExtractionRequest r, CancellationToken ct)
    {
        if (
            r.DurationMs <= 0
            || r.DurationMs > 1800000
            || !r.ObjectKey.StartsWith("originals/", StringComparison.Ordinal)
        )
            throw new ProviderException("invalid_extraction_request", 400);
        using var obj = await store.OpenAsync(r.ObjectKey, ct);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(
                new Uri(o.ContentEndpoint),
                $"contentunderstanding/analyzers/{Uri.EscapeDataString(o.AnalyzerId)}:analyzeBinary?api-version=2025-11-01"
            )
        );
        request.Headers.Add("Ocp-Apim-Subscription-Key", o.ContentKey);
        request.Content = new StreamContent(obj.ResponseStream);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await http.SendAsync(request, ct);
        await ProviderHttp.RequireSuccessAsync(response, ct);
        if (!response.Headers.TryGetValues("Operation-Location", out var locations))
            throw new ProviderException("missing_provider_operation");
        var operation = locations.Single();
        ValidateOperation(operation);
        return new("Running", operation, null, []);
    }

    public async Task<ExtractionResult> PollAsync(ExtractionPollRequest r, CancellationToken ct)
    {
        ValidateOperation(r.OperationUrl);
        using var request = new HttpRequestMessage(HttpMethod.Get, r.OperationUrl);
        request.Headers.Add("Ocp-Apim-Subscription-Key", o.ContentKey);
        using var response = await http.SendAsync(request, ct);
        using var result = await ProviderHttp.JsonAsync(response, ct);
        var status = result.RootElement.GetProperty("status").GetString();
        if (status is "Running" or "NotStarted" or "running" or "notStarted")
            return new("Running", r.OperationUrl, null, []);
        if (status is not "Succeeded" and not "succeeded")
            throw new ProviderException("extraction_failed");
        var raw = result.RootElement.GetRawText();
        return new("Succeeded", r.OperationUrl, raw, mapper.Map(raw, 0, r.DurationMs));
    }

    public void ValidateOperation(string url)
    {
        var endpoint = new Uri(o.ContentEndpoint);
        if (
            !Uri.TryCreate(url, UriKind.Absolute, out var operation)
            || operation.Scheme != "https"
            || !operation.Host.Equals(endpoint.Host, StringComparison.OrdinalIgnoreCase)
            || operation.Port != 443
            || operation.UserInfo.Length > 0
            || !operation.AbsolutePath.StartsWith(
                "/contentunderstanding/",
                StringComparison.Ordinal
            )
        )
            throw new ProviderException("invalid_operation_url", 400);
    }
}
