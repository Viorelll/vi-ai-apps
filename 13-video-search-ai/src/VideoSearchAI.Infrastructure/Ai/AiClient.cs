using System.Net.Http.Json;
using VideoSearchAI.Contracts.Dto;
using VideoSearchAI.Domain.Models;

namespace VideoSearchAI.Infrastructure.Ai;

public sealed class AiClient(HttpClient http)
{
    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(path, body, ct);
        if (!response.IsSuccessStatusCode)
            throw new AiHttpException((int)response.StatusCode, response.Headers.RetryAfter?.Delta);
        return await response.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new DomainException("invalid_ai_response", 502);
    }

    public Task<ExtractionResult> StartAsync(ExtractionRequest r, CancellationToken ct) =>
        PostAsync<ExtractionResult>("internal/extractions/start", r, ct);

    public Task<ExtractionResult> PollAsync(ExtractionPollRequest r, CancellationToken ct) =>
        PostAsync<ExtractionResult>("internal/extractions/poll", r, ct);

    public Task<RetrievalHit[]> RetrieveAsync(RetrieveRequest r, CancellationToken ct) =>
        PostAsync<RetrievalHit[]>("internal/retrieve", r, ct);

    public Task<GeneratedAnswer> GenerateAsync(GenerateRequest r, CancellationToken ct) =>
        PostAsync<GeneratedAnswer>("internal/generate", r, ct);

    public Task<System.Text.Json.JsonElement> IndexAsync(IndexRequest r, CancellationToken ct) =>
        PostAsync<System.Text.Json.JsonElement>("internal/index/upsert", r, ct);

    public Task<System.Text.Json.JsonElement> DeleteAsync(
        DeleteIndexRequest r,
        CancellationToken ct
    ) => PostAsync<System.Text.Json.JsonElement>("internal/index/delete", r, ct);
}
