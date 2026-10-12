using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using ProcureAI.Application.Abstractions;
using ProcureAI.Application.Exceptions;
using ProcureAI.Domain.DTOs;

namespace ProcureAI.Infrastructure.Ai;

public sealed class HttpAiGateway(HttpClient http, IConfiguration configuration) : IAiGateway
{
    public Task<DomainStepResult> StepAsync(DomainStepRequest request, CancellationToken ct) =>
        PostAsync<DomainStepResult>("internal/domain/step", request, ct);
    public Task<ExtractionResult> ExtractAsync(ExtractionRequest request, CancellationToken ct) =>
        PostAsync<ExtractionResult>("internal/extractions", request, ct);
    private async Task<T> PostAsync<T>(string path, object payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-Internal-Key", configuration["Internal:Key"]);
        request.Content = JsonContent.Create(payload);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var code = (int)response.StatusCode is 401 or 403 ? "gateway_authentication_failed" : "ai_provider_unavailable";
            try
            {
                using var problem = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                if (problem.RootElement.TryGetProperty("code", out var error) && error.GetString() is { } safeCode &&
                    safeCode.Length <= 80 && safeCode.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))
                    code = safeCode;
            }
            catch (System.Text.Json.JsonException) { }
            throw new AppException(code, (int)response.StatusCode == 429 ? 429 : (int)response.StatusCode >= 500 ? 503 : 422);
        }
        return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new AppException("empty_gateway_response", 422);
    }
}
