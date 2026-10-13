using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModerateAI.Ai.Options;
using ModerateAI.Domain.Dto;
using ModerateAI.Domain.Interfaces;
using ModerateAI.Domain.Models;
using ModerateAI.Domain.Services;

namespace ModerateAI.Ai.Providers;

public sealed class ContentSafetyAdapter(
    HttpClient client,
    IOptions<ProviderOptions> options,
    IObjectStore store
)
{
    public async Task<SafetySnapshot> AnalyzeAsync(AnalysisRequest request, CancellationToken ct)
    {
        var snapshots = new List<SafetySnapshot>();
        if (!string.IsNullOrWhiteSpace(request.Text))
            snapshots.Add(
                await SendAsync(
                    "text:analyze",
                    new
                    {
                        text = request.Text,
                        categories = PolicyEvaluator.Categories,
                        outputType = "FourSeverityLevels",
                    },
                    ct
                )
            );
        if (request.ImageObjectKey != null)
        {
            if (
                !request.ImageObjectKey.StartsWith("originals/", StringComparison.Ordinal)
                || request.ImageContentType is not ("image/png" or "image/jpeg")
            )
                throw new ProviderException("invalid_image_scope", false);
            var bytes = await store.ReadAsync(request.ImageObjectKey, ct);
            if (bytes.Length > 4 * 1024 * 1024)
                throw new ProviderException("image_too_large", false);
            snapshots.Add(
                await SendAsync(
                    "image:analyze",
                    new
                    {
                        image = new { content = Convert.ToBase64String(bytes) },
                        categories = PolicyEvaluator.Categories,
                        outputType = "FourSeverityLevels",
                    },
                    ct
                )
            );
        }
        return SafetyResponseMapper.Combine(snapshots);
    }

    public async Task ShieldAsync(string text, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(
                new Uri(options.Value.SafetyEndpoint),
                "contentsafety/text:shieldPrompt?api-version=" + SafetyResponseMapper.ApiVersion
            )
        );
        request.Headers.Add("Ocp-Apim-Subscription-Key", options.Value.SafetyApiKey);
        request.Content = JsonContent.Create(
            new { userPrompt = text, documents = Array.Empty<string>() }
        );
        using var response = await client.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (
            !json.RootElement.TryGetProperty("userPromptAnalysis", out var analysis)
            || !analysis.TryGetProperty("attackDetected", out var detected)
            || detected.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
        )
            throw new ProviderException("invalid_shield_response", false);
        if (detected.GetBoolean())
            throw new ProviderException("prompt_attack_detected", false);
    }

    private async Task<SafetySnapshot> SendAsync(
        string operation,
        object payload,
        CancellationToken ct
    )
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(
                new Uri(options.Value.SafetyEndpoint),
                "contentsafety/" + operation + "?api-version=" + SafetyResponseMapper.ApiVersion
            )
        );
        request.Headers.Add("Ocp-Apim-Subscription-Key", options.Value.SafetyApiKey);
        request.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return SafetyResponseMapper.Map(await response.Content.ReadAsStringAsync(ct));
    }

    public static Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            int status = (int)response.StatusCode;
            int retry = (int)(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 0);
            throw new ProviderException(
                "provider_http_" + status,
                status == 429 || status >= 500,
                retry
            );
        }
        return Task.CompletedTask;
    }
}
