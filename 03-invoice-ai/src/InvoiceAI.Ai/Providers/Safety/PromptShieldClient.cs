using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using InvoiceAI.Ai.Configuration;
using InvoiceAI.Ai.Providers.Extraction;
using InvoiceAI.Infrastructure.Ai;
namespace InvoiceAI.Ai.Providers.Safety;

public sealed class PromptShieldClient(HttpClient http, IOptions<SafetyOptions> options)
{
    public async Task CheckAsync(string prompt, IReadOnlyList<string> documents, CancellationToken ct)
    {
        var o = options.Value;
        if (!o.PromptShieldsEnabled)
            return;
        if (!Uri.TryCreate(o.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" || endpoint.Host.StartsWith("RESOURCE", StringComparison.OrdinalIgnoreCase) || o.ApiKey.StartsWith("dummy", StringComparison.OrdinalIgnoreCase))
            throw new GatewayException("SAFETY_CONFIGURATION_MISSING", false);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "contentsafety/text:shieldPrompt?api-version=2024-09-01"));
        request.Headers.Add("Ocp-Apim-Subscription-Key", o.ApiKey);
        request.Content = JsonContent.Create(new
        {
            userPrompt = prompt,
            documents
        });
        using var response = await http.SendAsync(request, ct);
        DocumentIntelligenceClient.Check(response);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = json.RootElement;
        if (root.GetProperty("userPromptAnalysis").GetProperty("attackDetected").GetBoolean() || root.GetProperty("documentsAnalysis").EnumerateArray().Any(x => x.GetProperty("attackDetected").GetBoolean()))
            throw new GatewayException("PROMPT_ATTACK_DETECTED", false);
    }
}
