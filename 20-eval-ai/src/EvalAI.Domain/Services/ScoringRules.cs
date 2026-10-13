using System.Text.Json;
using EvalAI.Domain.DTOs;
using EvalAI.Domain.Entities;
using EvalAI.Domain.Models;
namespace EvalAI.Domain.Services;

public static class ScoringRules
{
    public const string ChecksVersion = "checks-v1";
    public static CheckResult Check(string output, string expected, EvidenceSource[] sources)
    {
        var errors = new List<string>(); bool fields = false, citations = false;
        try
        {
            using var actual = JsonDocument.Parse(output); using var golden = JsonDocument.Parse(expected);
            fields = Matches(golden.RootElement, actual.RootElement);
            if (!fields) errors.Add("expected_fields_mismatch");
            citations = true;
            if (actual.RootElement.TryGetProperty("citations", out var refs))
            {
                if (refs.ValueKind != JsonValueKind.Array) citations = false;
                else foreach (var c in refs.EnumerateArray())
                {
                    if (!c.TryGetProperty("chunkId", out var id) || !Guid.TryParse(id.GetString(), out var guid) || !c.TryGetProperty("quote", out var q) || q.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(q.GetString()) || !sources.Any(s => s.ChunkId == guid && s.Text.Contains(q.GetString()!, StringComparison.Ordinal))) citations = false;
                }
            }
            if (sources.Length > 0 && (!actual.RootElement.TryGetProperty("citations", out var list) || list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)) citations = false;
            if (!citations) errors.Add("invalid_citations");
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException) { errors.Add("invalid_output_json"); }
        return new(fields && citations, fields, citations, errors.ToArray());
    }
    public static bool Matches(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind != actual.ValueKind) return false;
        return expected.ValueKind switch
        {
            JsonValueKind.Object => expected.EnumerateObject().All(p => actual.TryGetProperty(p.Name, out var a) && Matches(p.Value, a)),
            JsonValueKind.Array => expected.GetArrayLength() == actual.GetArrayLength() && expected.EnumerateArray().Zip(actual.EnumerateArray()).All(x => Matches(x.First, x.Second)),
            JsonValueKind.Number => expected.TryGetDecimal(out var e) && actual.TryGetDecimal(out var a) && e == a,
            JsonValueKind.String => expected.GetString() == actual.GetString(),
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
            _ => false
        };
    }
    public static decimal? KnownTextCost(TokenUsage usage, CostRate? rate)
    {
        if (usage.Input < 0 || usage.Output < 0) throw new ArgumentException("invalid_usage");
        if (rate is null || rate.InputPerMillion is null || rate.OutputPerMillion is null) return null;
        if (rate.InputPerMillion < 0 || rate.OutputPerMillion < 0) throw new ArgumentException("invalid_rate");
        return (usage.Input * rate.InputPerMillion + usage.Output * rate.OutputPerMillion) / 1000000m;
    }
    public static void RequireSameBaseline(EvalScope current, EvalScope baseline)
    {
        if (current != baseline) throw new AppException(409, "incompatible_baseline", "Baseline requires identical dataset, evidence, checks, rubric and safety versions.");
    }
    public static long Percentile(IEnumerable<long> values, decimal percentile)
    {
        var sorted = values.Order().ToArray(); return sorted.Length == 0 ? 0 : sorted[Math.Max(0, (int)Math.Ceiling(percentile * sorted.Length) - 1)];
    }
}
