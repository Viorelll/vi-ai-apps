using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModerateAI.Ai.Options;
using ModerateAI.Domain.Dto;
using ModerateAI.Domain.Models;

namespace ModerateAI.Ai.Providers;

public sealed class FoundryExplainer(HttpClient client, IOptions<ProviderOptions> options)
{
    public async Task<(string Explanation, long InputTokens, long OutputTokens)> ExplainAsync(
        SafetySnapshot snapshot,
        PolicyResult policy,
        CancellationToken ct
    )
    {
        // The model sees only validated scores and the immutable decision, never the submitted text/image.
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(options.Value.OpenAiEndpoint), "responses")
        );
        request.Headers.Add("api-key", options.Value.ApiKey);
        request.Content = JsonContent.Create(
            new
            {
                model = options.Value.ChatDeployment,
                store = false,
                max_output_tokens = 600,
                instructions = "Explain the supplied local moderation policy decision in one short Romanian sentence using only category names and severity numbers. Do not reproduce harmful content, invent evidence, or propose a different decision. Return the required JSON.",
                input = JsonSerializer.Serialize(
                    new
                    {
                        snapshot.OutputScale,
                        snapshot.Scores,
                        policy.Decision,
                        policy.PolicyVersion,
                        policy.Reasons,
                    }
                ),
                text = new
                {
                    format = new
                    {
                        type = "json_schema",
                        name = "moderation_explanation",
                        strict = true,
                        schema = new
                        {
                            type = "object",
                            properties = new { explanation = new { type = "string" } },
                            required = new[] { "explanation" },
                            additionalProperties = false,
                        },
                    },
                },
            }
        );
        using var response = await client.SendAsync(request, ct);
        await ContentSafetyAdapter.EnsureSuccessAsync(response, ct);
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = json.RootElement;
            if (!root.TryGetProperty("status", out var status) || status.GetString() != "completed")
                throw new ProviderException("incomplete_foundry_response", false);
            var texts = new List<string>();
            foreach (var output in root.GetProperty("output").EnumerateArray())
            {
                if (output.GetProperty("type").GetString() != "message")
                    throw new ProviderException("unexpected_foundry_output", false);
                foreach (var content in output.GetProperty("content").EnumerateArray())
                    if (content.GetProperty("type").GetString() == "output_text")
                        texts.Add(content.GetProperty("text").GetString()!);
                    else
                        throw new ProviderException("foundry_refusal", false);
            }
            if (texts.Count != 1)
                throw new ProviderException("invalid_foundry_response", false);
            using var result = JsonDocument.Parse(texts[0]);
            if (result.RootElement.EnumerateObject().Count() != 1)
                throw new ProviderException("invalid_explanation", false);
            string explanation = result.RootElement.GetProperty("explanation").GetString() ?? "";
            if (string.IsNullOrWhiteSpace(explanation) || explanation.Length > 1000)
                throw new ProviderException("invalid_explanation", false);
            var usage = root.GetProperty("usage");
            long input = usage.GetProperty("input_tokens").GetInt64(),
                outputTokens = usage.GetProperty("output_tokens").GetInt64();
            if (input < 0 || outputTokens < 0 || input + outputTokens > 15000)
                throw new ProviderException("token_budget_exceeded", false);
            return (explanation, input, outputTokens);
        }
        catch (Exception e)
            when (e
                    is JsonException
                        or KeyNotFoundException
                        or InvalidOperationException
                        or FormatException
            )
        {
            throw new ProviderException("invalid_foundry_json", false);
        }
    }
}
