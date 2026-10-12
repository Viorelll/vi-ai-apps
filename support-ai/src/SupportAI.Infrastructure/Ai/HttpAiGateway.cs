using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using SupportAI.Application.Abstractions;
using SupportAI.Domain.DTOs;
using SupportAI.Domain.Models;
namespace SupportAI.Infrastructure.Ai;

public sealed class HttpAiGateway(HttpClient http, IConfiguration config) : IAiGateway
{
    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-Internal-Key", config["Internal:Key"]);
        request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var code = response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ? "ai_rate_limited" : "ai_provider_error";
            var retry = ProviderRetry.Seconds(response);
            try
            {
                using var error = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (error.RootElement.TryGetProperty("code", out var c) && c.GetString() is { Length: > 0 and < 100 } safeCode)
                    code = safeCode;
                if (error.RootElement.TryGetProperty("retryAfterSeconds", out var r) && r.ValueKind == System.Text.Json.JsonValueKind.Number && r.TryGetInt32(out var seconds))
                    retry = Math.Clamp(seconds, 0, 86400);
            }
            catch (System.Text.Json.JsonException) { }
            throw new SupportException(code, (int)response.StatusCode, retry);
        }
        return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new SupportException("invalid_ai_response", 502);
    }
    public Task<DomainStepResult> StepAsync(DomainStepRequest request, CancellationToken ct) => PostAsync<DomainStepResult>("internal/domain/step", request, ct);
    public Task<ExtractionResult> StartAsync(ExtractionRequest request, CancellationToken ct) => PostAsync<ExtractionResult>("internal/extractions/start", request, ct);
    public Task<ExtractionResult> PollAsync(string url, CancellationToken ct) => PostAsync<ExtractionResult>("internal/extractions/poll", new ExtractionPoll(url), ct);
}
