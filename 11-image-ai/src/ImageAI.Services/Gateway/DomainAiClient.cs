using System.Net.Http.Json;
using ImageAI.Contracts.Dto;
using Microsoft.Extensions.Options;

namespace ImageAI.Services.Gateway;

public sealed class DomainAiClient(HttpClient http, IOptions<GatewayOptions> options)
{
    public async Task<GatewayResult> GenerateAsync(GatewayRequest input, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/images/generate");
        request.Headers.Add("X-Internal-Key", options.Value.InternalApiKey);
        request.Headers.Add("X-Correlation-Id", input.OperationId.ToString());
        request.Content = JsonContent.Create(input);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            string code = "ai_unavailable";
            try
            {
                var error = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(
                    ct
                );
                if (error.TryGetProperty("code", out var c))
                    code = c.GetString() ?? code;
            }
            catch (System.Text.Json.JsonException) { }
            if (code.Length > 80 || code.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_')))
                code = "ai_unavailable";
            int retry = (int)
                Math.Clamp(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 5, 1, 300);
            throw new GatewayException(
                code,
                response.StatusCode == System.Net.HttpStatusCode.TooManyRequests,
                retry,
                (int)response.StatusCode >= 500
                    && code is not ("provider_not_configured" or "chat_deployment_not_configured")
            );
        }
        return await response.Content.ReadFromJsonAsync<GatewayResult>(ct)
            ?? throw new GatewayException("invalid_gateway_response", uncertain: true);
    }
}
