using System.Net.Http.Json;
using BuildBudgetAI.Domain.Dto;
using BuildBudgetAI.Domain.Interfaces;
using BuildBudgetAI.Domain.Models;

namespace BuildBudgetAI.Infrastructure;

public sealed class AiHttpClient(HttpClient http) : IAiClient
{
    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        using var r = await http.PostAsJsonAsync(path, body, ct);
        if (!r.IsSuccessStatusCode)
            throw new DomainException(
                $"ai_http_{(int)r.StatusCode}",
                (int)r.StatusCode >= 500 ? 503 : 422
            );
        return await r.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new DomainException("invalid_ai_response", 502);
    }

    public Task<OfferDraft> ExtractOffersAsync(OfferExtractionRequest r, CancellationToken ct) =>
        PostAsync<OfferDraft>("internal/offers", r, ct);

    public Task<ExtractionResult> ExtractAsync(ExtractionRequest r, CancellationToken ct) =>
        PostAsync<ExtractionResult>("internal/extract", r, ct);

    public Task<GenerateResult> GenerateAsync(GenerateRequest r, CancellationToken ct) =>
        PostAsync<GenerateResult>("internal/generate", r, ct);
}
