using System.Net.Http.Json;
using IncidentAI.Application.Contracts;

namespace IncidentAI.Api.Services;

public sealed class AiGatewayClient(HttpClient http, IConfiguration configuration)
{
    public async Task<DiagnosisResult> DiagnoseAsync(DiagnosisInput input, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/diagnose") { Content = JsonContent.Create(input) };
        request.Headers.TryAddWithoutValidation("X-Internal-Key", configuration["Internal:Key"] ?? "local-development-key");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiagnosisResult>(cancellationToken: ct)
            ?? throw new InvalidOperationException("AI gateway returned an empty response.");
    }
}
