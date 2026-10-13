using System.Net.Http.Json;
using EvalAI.Domain.DTOs;
using EvalAI.Domain.Models;
using EvalAI.Infrastructure.Abstractions;
namespace EvalAI.Infrastructure.Ai;

public sealed class DomainAiClient(HttpClient http) : IAiClient
{
    public Task<AiStepResult> StepAsync(AiStepRequest r, CancellationToken ct) => Post<AiStepResult>("internal/domain/step", r, ct);
    public Task<ExtractionResult> StartAsync(ExtractionStart r, CancellationToken ct) => Post<ExtractionResult>("internal/extractions/start", r, ct);
    public Task<ExtractionResult> PollAsync(ExtractionPoll r, CancellationToken ct) => Post<ExtractionResult>("internal/extractions/poll", r, ct);
    public Task<AiStepResult> ChatAsync(ChatAiRequest r, CancellationToken ct) => Post<AiStepResult>("internal/chat", r, ct);
    private async Task<T> Post<T>(string url, object body, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(url, body, JsonDefaults.Options, ct);
        if (!response.IsSuccessStatusCode)
        {
            string code = "gateway_http_" + (int)response.StatusCode; int? retry = null;
            try { using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); if (json.RootElement.TryGetProperty("code", out var c)) code = c.GetString() ?? code; if (json.RootElement.TryGetProperty("retryAfterSeconds", out var r) && r.TryGetInt32(out var seconds)) retry = seconds; } catch (System.Text.Json.JsonException) { }
            throw new AppException((int)response.StatusCode, code, "AI gateway failed. Check server configuration or retry a transient failure.", retry);
        }
        return await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Options, ct) ?? throw new AppException(502, "empty_gateway_response", "Gateway returned no response.");
    }
}
