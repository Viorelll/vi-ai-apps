using System.Net.Http.Json;
using AccessReadAI.Contracts;

namespace AccessReadAI.Services.Clients;

public sealed class DomainAiClient(HttpClient http)
{
    private async Task<T> Post<T>(string path, object input, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(path, input, ct);
        if (!response.IsSuccessStatusCode)
        {
            var fault = await response.Content.ReadFromJsonAsync<
                Dictionary<string, System.Text.Json.JsonElement>
            >(ct);
            var code = fault?.GetValueOrDefault("code").GetString() ?? "gateway_failure";
            throw new ProviderFault(
                code,
                (int)response.StatusCode is 429 or >= 500 && code != "provider_not_configured",
                (int)(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 0)
            );
        }
        return await response.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new ProviderFault("empty_gateway_result");
    }

    public Task<ExtractionResult> ExtractAsync(ExtractionRequest input, CancellationToken ct) =>
        Post<ExtractionResult>("internal/extract", input, ct);

    public Task<TransformResult> TransformAsync(TransformRequest input, CancellationToken ct) =>
        Post<TransformResult>("internal/transform", input, ct);

    public Task<NarrateResult> NarrateAsync(NarrateRequest input, CancellationToken ct) =>
        Post<NarrateResult>("internal/narrate", input, ct);

    public Task<GroundedAnswer> AnswerAsync(GroundedRequest input, CancellationToken ct) =>
        Post<GroundedAnswer>("internal/answer", input, ct);
}
