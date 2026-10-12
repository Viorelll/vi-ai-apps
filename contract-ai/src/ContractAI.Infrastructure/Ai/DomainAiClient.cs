using System.Net.Http.Json;
using ContractAI.Domain.DTOs;
using ContractAI.Domain.Models;
using ContractAI.Infrastructure.Abstractions;

namespace ContractAI.Infrastructure.Ai;

public sealed class DomainAiClient(HttpClient http) : IAiClient
{
    public Task<AiResult> StepAsync(AiRequest request, CancellationToken ct) =>
        PostAsync<AiResult>("internal/domain/step", request, ct);

    public Task<ExtractionResult> StartAsync(ExtractionStart request, CancellationToken ct) =>
        PostAsync<ExtractionResult>("internal/extractions/start", request, ct);

    public Task<ExtractionResult> PollAsync(ExtractionPoll request, CancellationToken ct) =>
        PostAsync<ExtractionResult>("internal/extractions/poll", request, ct);

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(path, body, ct);
        if (!response.IsSuccessStatusCode)
        {
            var code = "ai_gateway_failure";
            int? retryAfterSeconds = null;
            try
            {
                var p = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(ct);
                if (p.TryGetProperty("code", out var c))
                    code = c.GetString() ?? code;
                if (
                    p.TryGetProperty("retryAfterSeconds", out var retry)
                    && retry.ValueKind == System.Text.Json.JsonValueKind.Number
                )
                    retryAfterSeconds = retry.GetInt32();
            }
            catch (System.Text.Json.JsonException) { }
            throw new AppException(
                (int)response.StatusCode,
                code,
                "AI gateway could not complete the operation. Check server-side configuration.",
                retryAfterSeconds
            );
        }
        return await response.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new AppException(502, "empty_ai_result", "AI returned an empty response.");
    }
}
