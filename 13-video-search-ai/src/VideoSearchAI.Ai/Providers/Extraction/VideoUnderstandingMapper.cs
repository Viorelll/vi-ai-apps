using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using VideoSearchAI.Contracts.Dto;

namespace VideoSearchAI.Ai.Providers.Extraction;

public sealed partial class VideoUnderstandingMapper
{
    public EvidenceDto[] Map(string raw, long offset, long duration)
    {
        using var json = JsonDocument.Parse(raw);
        var root = json.RootElement;
        if (root.TryGetProperty("result", out var result))
            root = result;
        if (
            !root.TryGetProperty("contents", out var contents)
            || contents.ValueKind != JsonValueKind.Array
        )
            throw new ProviderException("cu_schema_mismatch");
        var sources = new List<EvidenceDto>();
        var ordinal = 0;
        foreach (var content in contents.EnumerateArray())
        {
            var markdown = content.TryGetProperty("markdown", out var md)
                ? md.GetString() ?? ""
                : "";
            var phrases = new List<EvidenceDto>();
            if (
                content.TryGetProperty("transcriptPhrases", out var details)
                && details.GetArrayLength() > 0
            )
            {
                foreach (var phrase in details.EnumerateArray())
                {
                    var start = checked(phrase.GetProperty("startTimeMs").GetInt64() + offset);
                    var end = checked(phrase.GetProperty("endTimeMs").GetInt64() + offset);
                    Validate(start, end, duration);
                    var text = phrase.GetProperty("text").GetString() ?? "";
                    if (text.Length > 0)
                        phrases.Add(new("", start, end, text, null, []));
                }
            }
            else
                foreach (Match match in Vtt().Matches(markdown))
                {
                    var start = checked(Time(match.Groups[1].Value) + offset);
                    var end = checked(Time(match.Groups[2].Value) + offset);
                    Validate(start, end, duration);
                    var text = Speaker().Replace(match.Groups[3].Value, " ").Trim();
                    if (text.Length > 0)
                        phrases.Add(new("", start, end, text, null, []));
                }
            // Preserve sentence boundaries, chunk up to 60 seconds with bounded text.
            foreach (var phrase in phrases.OrderBy(x => x.StartMs))
            {
                var previous = sources.LastOrDefault();
                if (
                    previous != null
                    && previous.Transcript.Length > 0
                    && phrase.EndMs - previous.StartMs <= 60000
                    && phrase.StartMs - previous.EndMs <= 15000
                    && previous.Transcript.Length + phrase.Transcript.Length < 16000
                )
                    sources[^1] = previous with
                    {
                        EndMs = Math.Max(previous.EndMs, phrase.EndMs),
                        Transcript = previous.Transcript + " " + phrase.Transcript,
                    };
                else
                    sources.Add(phrase with { SourceId = $"transcript:{ordinal++}" });
            }
            var scenes = Scene().Matches(markdown);
            if (scenes.Count > 0)
            {
                foreach (Match scene in scenes)
                {
                    var start = checked(Time(scene.Groups[1].Value) + offset);
                    var end = checked(Time(scene.Groups[2].Value) + offset);
                    Validate(start, end, duration);
                    AddScene(start, end, scene.Groups[3].Value);
                }
            }
            else if (
                content.TryGetProperty("startTimeMs", out var startMs)
                && content.TryGetProperty("endTimeMs", out var endMs)
            )
            {
                var start = checked(startMs.GetInt64() + offset);
                var end = checked(endMs.GetInt64() + offset);
                Validate(start, end, duration);
                AddScene(start, end, markdown);
            }
        }
        if (sources.Count == 0)
            throw new ProviderException("cu_no_temporal_evidence");
        return sources.OrderBy(x => x.StartMs).ToArray();
        void AddScene(long start, long end, string markdown)
        {
            var description = markdown
                .Split("Transcript", StringSplitOptions.None)[0]
                .Split("Key Frames", StringSplitOptions.None)[0]
                .Trim();
            // Scene descriptions are visual evidence, but are not invented frame-specific descriptions.
            if (description.Length > 0)
                sources.Add(new($"scene:{ordinal++}", start, end, "", description, []));
        }
    }

    private static void Validate(long start, long end, long duration)
    {
        if (start < 0 || end <= start || end > duration)
            throw new ProviderException("invalid_timestamp");
    }

    private static long Time(string text)
    {
        var parts = text.Split(':');
        decimal seconds = decimal.Parse(parts[^1], CultureInfo.InvariantCulture);
        seconds += 60 * decimal.Parse(parts[^2], CultureInfo.InvariantCulture);
        if (parts.Length == 3)
            seconds += 3600 * decimal.Parse(parts[0], CultureInfo.InvariantCulture);
        return checked((long)(seconds * 1000));
    }

    [GeneratedRegex(
        @"((?:\d{2}:)?\d{2}:\d{2}\.\d{3})\s*-->\s*((?:\d{2}:)?\d{2}:\d{2}\.\d{3})[^\S\r\n]*\r?\n([^\r\n]+(?:\r?\n(?!\r?\n|\d{2}:|Key Frames|#)[^\r\n]+)*)"
    )]
    private static partial Regex Vtt();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Speaker();

    [GeneratedRegex(
        @"(?ms)^#+ Video:\s*((?:\d{2}:)?\d{2}:\d{2}\.\d{3})\s*=>\s*((?:\d{2}:)?\d{2}:\d{2}\.\d{3})[^\r\n]*\r?\n(.*?)(?=^#+ Video:|\z)"
    )]
    private static partial Regex Scene();
}
