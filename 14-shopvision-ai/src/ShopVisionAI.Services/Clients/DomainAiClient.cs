using System.Net.Http.Json;
using ShopVisionAI.Contracts.Dto;
using ShopVisionAI.Infrastructure.Http;

namespace ShopVisionAI.Services.Clients;

public sealed class DomainAiClient(HttpClient http)
{
    public async Task<VisionResponse> InspectAsync(VisionRequest input, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("internal/vision", input, ct);
        response.EnsureProviderSuccess();
        return await response.Content.ReadFromJsonAsync<VisionResponse>(ct)
            ?? throw new InvalidOperationException("empty_ai_response");
    }

    public async Task<GroundedResponse> GenerateAsync(GroundedRequest input, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("internal/grounded", input, ct);
        response.EnsureProviderSuccess();
        return await response.Content.ReadFromJsonAsync<GroundedResponse>(ct)
            ?? throw new InvalidOperationException("empty_grounded_response");
    }
}
