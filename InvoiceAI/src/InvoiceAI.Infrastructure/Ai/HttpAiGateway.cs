using System.Net.Http.Json;
using InvoiceAI.Contracts.Dto;
using InvoiceAI.Services.Abstractions;
namespace InvoiceAI.Infrastructure.Ai;

public sealed class HttpAiGateway(HttpClient http) : IAiGateway
{
    private async Task<T> PostAsync<T>(string path, object request, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(path, request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var transient = (int)response.StatusCode is 429 or >= 500;
            var code = "AI_GATEWAY_ERROR";
            long inputTokens = 0, outputTokens = 0;
            try
            {
                var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(ct);
                if (json.TryGetProperty("code", out var v))
                    code = v.GetString() ?? code;
                if (json.TryGetProperty("inputTokens", out var input))
                    inputTokens = input.GetInt64();
                if (json.TryGetProperty("outputTokens", out var output))
                    outputTokens = output.GetInt64();
            }
            catch (System.Text.Json.JsonException) { }
            if (code is "SAFETY_CONFIGURATION_MISSING" or "PROMPT_ATTACK_DETECTED" or "AI_CONFIGURATION_MISSING" or "PROVIDER_AUTH_FAILED" or "PROVIDER_REQUEST_INVALID")
                transient = false;
            throw new GatewayException(code, transient, (int)(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 2), inputTokens, outputTokens);
        }
        return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new GatewayException("AI_CONTRACT_INVALID", false);
    }
    public Task<StartExtractionResponse> StartAsync(StartExtractionRequest r, CancellationToken ct) => PostAsync<StartExtractionResponse>("internal/extractions/start", r, ct);
    public Task<PollExtractionResponse> PollAsync(PollExtractionRequest r, CancellationToken ct) => PostAsync<PollExtractionResponse>("internal/extractions/poll", r, ct);
    public Task<ExplanationDto> ExplainAsync(ExplainRequest r, CancellationToken ct) => PostAsync<ExplanationDto>("internal/domain/step", r, ct);
    public async Task<bool> ReadyAsync(CancellationToken ct)
    {
        using var response = await http.GetAsync("health/ready", ct);
        return response.IsSuccessStatusCode;
    }
}
