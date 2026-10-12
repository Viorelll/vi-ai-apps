using System.Net.Http.Json;
using System.Text.Json;
using SupportAI.Domain.DTOs;
using SupportAI.Domain.Models;
namespace SupportAI.Ai.Services;

public sealed class PromptShieldService(HttpClient http, IConfiguration config)
{
    public async Task CheckAsync(DomainStepRequest request, CancellationToken ct)
    {
        if (!config.GetValue("Safety:PromptShieldsEnabled", false))
            return;
        if (!Uri.TryCreate(config["Safety:Endpoint"], UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" || string.IsNullOrWhiteSpace(config["Safety:ApiKey"]) || config["Safety:ApiKey"]!.StartsWith("replace-"))
            throw new SupportException("safety_not_configured", 503);
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "contentsafety/text:shieldPrompt?api-version=2024-09-01"));
        message.Headers.Add("Ocp-Apim-Subscription-Key", config["Safety:ApiKey"]);
        message.Content = JsonContent.Create(new
        {
            userPrompt = request.Text,
            documents = request.Sources.Select(x => x.Text).ToArray()
        });
        using var response = await http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
            throw new SupportException("safety_request_failed", (int)response.StatusCode, ProviderRetry.Seconds(response));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = body.RootElement;
        if (root.GetProperty("userPromptAnalysis").GetProperty("attackDetected").GetBoolean() || root.GetProperty("documentsAnalysis").EnumerateArray().Any(x => x.GetProperty("attackDetected").GetBoolean()))
            throw new SupportException("prompt_attack_detected", 422);
    }
}
