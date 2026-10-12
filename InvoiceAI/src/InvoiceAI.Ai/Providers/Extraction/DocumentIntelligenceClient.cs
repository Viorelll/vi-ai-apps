using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using InvoiceAI.Ai.Configuration;
using InvoiceAI.Contracts.Dto;
using InvoiceAI.Infrastructure.Ai;
using InvoiceAI.Services.Abstractions;
namespace InvoiceAI.Ai.Providers.Extraction;

public sealed class DocumentIntelligenceClient(HttpClient http, IObjectStore storage, IOptions<ProviderOptions> options)
{
    private readonly ProviderOptions o = options.Value;
    public async Task<StartExtractionResponse> StartAsync(StartExtractionRequest request, CancellationToken ct)
    {
        if (request.Kind is not ("invoice" or "receipt") || !request.ObjectKey.StartsWith($"originals/{request.DocumentId:N}/", StringComparison.Ordinal))
            throw new GatewayException("EXTRACTION_INPUT_INVALID", false);
        if (!o.Configured)
            throw new GatewayException("AI_CONFIGURATION_MISSING", false);
        await using var stream = await storage.OpenAsync(request.ObjectKey, ct);
        var model = request.Kind == "invoice" ? "prebuilt-invoice" : "prebuilt-receipt";
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(o.DocumentEndpoint), $"documentintelligence/documentModels/{model}:analyze?api-version=2024-11-30&stringIndexType=utf16CodeUnit"));
        message.Headers.Add("Ocp-Apim-Subscription-Key", o.DocumentKey);
        message.Content = new StreamContent(stream);
        message.Content.Headers.ContentType = new("application/octet-stream");
        using var response = await http.SendAsync(message, ct);
        Check(response);
        var url = response.Headers.GetValues("Operation-Location").Single();
        ValidateOperation(url);
        return new(url);
    }
    public async Task<PollExtractionResponse> PollAsync(PollExtractionRequest request, CancellationToken ct)
    {
        var url = ValidateOperation(request.OperationUrl);
        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        message.Headers.Add("Ocp-Apim-Subscription-Key", o.DocumentKey);
        using var response = await http.SendAsync(message, ct);
        Check(response);
        using var root = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var status = root.RootElement.GetProperty("status").GetString();
        return status switch
        {
            "succeeded" => new("succeeded", DocumentIntelligenceParser.Parse(request.DocumentId, request.Kind, root.RootElement)),
            "running" or "notStarted" => new("running", null, (int)Math.Clamp(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 2, 1, 60)),
            _ => throw new GatewayException("EXTRACTION_FAILED", false)
        };
    }
    public Uri ValidateOperation(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != new Uri(o.DocumentEndpoint).Host || uri.Port != 443 || !string.IsNullOrEmpty(uri.UserInfo) || !uri.AbsolutePath.StartsWith("/documentintelligence/documentModels/", StringComparison.Ordinal))
            throw new GatewayException("OPERATION_URL_INVALID", false);
        return uri;
    }
    public static void Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;
        var status = (int)response.StatusCode;
        var code = status is 401 or 403 ? "PROVIDER_AUTH_FAILED" : status is 400 or 404 ? "PROVIDER_REQUEST_INVALID" : "PROVIDER_UNAVAILABLE";
        throw new GatewayException(code, status is 429 or >= 500, (int)(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 2));
    }
}
