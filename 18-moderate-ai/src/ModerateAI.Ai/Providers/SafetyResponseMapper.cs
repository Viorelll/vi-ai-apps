using System.Text.Json;
using ModerateAI.Domain.Dto;
using ModerateAI.Domain.Models;
using ModerateAI.Domain.Services;

namespace ModerateAI.Ai.Providers;

public static class SafetyResponseMapper
{
    public const string ApiVersion = "2024-09-01";

    public static SafetySnapshot Map(string raw)
    {
        try
        {
            using var json = JsonDocument.Parse(raw);
            var findings = new List<SafetyScore>();
            if (
                !json.RootElement.TryGetProperty("categoriesAnalysis", out var categories)
                || categories.ValueKind != JsonValueKind.Array
            )
                return new("AzureContentSafety", ApiVersion, "FourSeverityLevels", [], false, raw);
            foreach (var element in categories.EnumerateArray())
            {
                if (
                    !element.TryGetProperty("category", out var category)
                    || category.ValueKind != JsonValueKind.String
                    || !element.TryGetProperty("severity", out var severity)
                    || severity.ValueKind != JsonValueKind.Number
                    || !severity.TryGetInt32(out int value)
                )
                    continue;
                findings.Add(new(category.GetString()!, value));
            }
            bool complete =
                findings.Count == 4
                && PolicyEvaluator.Categories.All(c => findings.Count(x => x.Category == c) == 1)
                && findings.All(x => new[] { 0, 2, 4, 6 }.Contains(x.Severity));
            return new(
                "AzureContentSafety",
                ApiVersion,
                "FourSeverityLevels",
                findings,
                complete,
                raw
            );
        }
        catch (JsonException)
        {
            throw new ProviderException("invalid_safety_json", false);
        }
    }

    public static SafetySnapshot Combine(IReadOnlyList<SafetySnapshot> snapshots)
    {
        if (snapshots.Count == 1)
            return snapshots[0];
        var scores = PolicyEvaluator
            .Categories.Where(c => snapshots.Any(s => s.Scores.Any(x => x.Category == c)))
            .Select(c => new SafetyScore(
                c,
                snapshots.SelectMany(s => s.Scores).Where(x => x.Category == c).Max(x => x.Severity)
            ))
            .ToArray();
        return new(
            "AzureContentSafety",
            ApiVersion,
            "FourSeverityLevels",
            scores,
            snapshots.Count > 0
                && snapshots.All(s =>
                    s.Complete
                    && s.OutputScale == "FourSeverityLevels"
                    && s.ApiVersion == ApiVersion
                ),
            JsonSerializer.Serialize(
                new
                {
                    Modalities = snapshots.Select(s =>
                        JsonSerializer.Deserialize<JsonElement>(s.RawJson)
                    ),
                }
            )
        );
    }
}
