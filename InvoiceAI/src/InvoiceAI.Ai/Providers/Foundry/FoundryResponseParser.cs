using System.Text.Json;
using InvoiceAI.Contracts.Dto;
using InvoiceAI.Infrastructure.Ai;
namespace InvoiceAI.Ai.Providers.Foundry;

public static class FoundryResponseParser
{
    public static ExplanationDto Parse(JsonElement root, IReadOnlySet<string> allowed, string deployment, long inputTokens, long outputTokens)
    {
        if (!root.TryGetProperty("status", out var status) || status.GetString() != "completed")
            throw new GatewayException("MODEL_INCOMPLETE", false);
        var text = string.Concat(root.GetProperty("output").EnumerateArray().Where(x => x.GetProperty("type").GetString() == "message").SelectMany(x => x.GetProperty("content").EnumerateArray()).Where(x => x.GetProperty("type").GetString() == "output_text").Select(x => x.GetProperty("text").GetString()));
        try
        {
            using var json = JsonDocument.Parse(text);
            var v = json.RootElement;
            var summary = v.GetProperty("summary").GetString();
            var category = v.GetProperty("suggestedCategory").ValueKind == JsonValueKind.Null ? null : v.GetProperty("suggestedCategory").GetString();
            var sources = v.GetProperty("sourceIds").EnumerateArray().Select(x => x.GetString()!).ToArray();
            if (string.IsNullOrWhiteSpace(summary) || summary.Length > 8000 || category?.Length > 200 || sources.Length > 100 || sources.Any(x => !allowed.Contains(x)))
                throw new GatewayException("MODEL_OUTPUT_INVALID", false);
            return new(summary, category, sources, deployment, inputTokens, outputTokens);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException) { throw new GatewayException("MODEL_OUTPUT_INVALID", false); }
    }
}
