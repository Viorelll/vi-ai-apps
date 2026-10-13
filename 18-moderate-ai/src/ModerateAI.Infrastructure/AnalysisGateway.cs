using System.Net.Http.Json;
using System.Text.Json;
using ModerateAI.Domain.Dto;
using ModerateAI.Domain.Interfaces;
using ModerateAI.Domain.Models;

namespace ModerateAI.Infrastructure;

public sealed class AnalysisGateway(HttpClient client) : IAnalysisGateway
{
    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync(
            "internal/moderation/analyze",
            request,
            ct
        );
        if (!response.IsSuccessStatusCode)
        {
            int status = (int)response.StatusCode;
            string code = "ai_gateway_" + status;
            try
            {
                using var detail = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (
                    detail.RootElement.TryGetProperty("code", out var value)
                    && value.ValueKind == JsonValueKind.String
                )
                {
                    var candidate = value.GetString()!;
                    if (
                        candidate.Length <= 80
                        && candidate.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
                    )
                        code = candidate;
                }
            }
            catch (JsonException) { }
            throw new ProviderException(
                code,
                status == 429 || status >= 500,
                (int)(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 0)
            );
        }
        return await response.Content.ReadFromJsonAsync<AnalysisResult>(ct)
            ?? throw new ProviderException("invalid_gateway_response", false);
    }
}
