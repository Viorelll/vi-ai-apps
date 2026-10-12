using System.Text.Json;
using VideoSearchAI.Ai.Providers;
using VideoSearchAI.Contracts.Dto;

namespace VideoSearchAI.Ai.Tools;

public static class GroundedTools
{
    public static object[] Definitions =>
        [
            Tool(
                "search_video_segments",
                "Search the already-authorized temporal evidence",
                new { query = new { type = "string" } },
                new[] { "query" }
            ),
            Tool(
                "read_segment_context",
                "Read one permitted segment",
                new { segmentId = new { type = "string" } },
                new[] { "segmentId" }
            ),
            Tool(
                "inspect_frame_evidence",
                "Read visual description evidence; no invented frames",
                new { segmentId = new { type = "string" } },
                new[] { "segmentId" }
            ),
            Tool(
                "resolve_timestamp",
                "Get validated original-video timestamps",
                new { segmentId = new { type = "string" } },
                new[] { "segmentId" }
            ),
        ];

    private static object Tool(
        string name,
        string description,
        object properties,
        string[] required
    ) =>
        new
        {
            type = "function",
            name,
            description,
            parameters = new
            {
                type = "object",
                properties,
                required,
                additionalProperties = false,
            },
            strict = true,
        };

    public static string Execute(string name, string arguments, GroundingSource[] sources)
    {
        if (arguments.Length > 4000)
            throw new ProviderException("tool_arguments_limit");
        using var json = JsonDocument.Parse(arguments);
        var a = json.RootElement;
        if (name == "search_video_segments")
        {
            var query = a.GetProperty("query").GetString() ?? "";
            if (query.Length > 2000)
                throw new ProviderException("tool_arguments_limit");
            return JsonSerializer.Serialize(
                sources
                    .Where(s =>
                        (s.Segment.Transcript + " " + s.Segment.VisualText).Contains(
                            query,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    .Take(8)
            );
        }
        if (
            name
            is not "read_segment_context"
                and not "inspect_frame_evidence"
                and not "resolve_timestamp"
        )
            throw new ProviderException("tool_not_allowed");
        if (!Guid.TryParse(a.GetProperty("segmentId").GetString(), out var id))
            throw new ProviderException("invalid_tool_source");
        var s =
            sources.SingleOrDefault(x => x.Segment.Id == id)
            ?? throw new ProviderException("invalid_tool_source");
        return name switch
        {
            "read_segment_context" => JsonSerializer.Serialize(s),
            "inspect_frame_evidence" => JsonSerializer.Serialize(
                new
                {
                    s.Label,
                    s.Segment.Id,
                    s.Segment.VisualText,
                    s.Segment.StartMs,
                    s.Segment.EndMs,
                }
            ),
            _ => JsonSerializer.Serialize(
                new
                {
                    s.Label,
                    s.Segment.Id,
                    s.Segment.StartMs,
                    s.Segment.EndMs,
                }
            ),
        };
    }
}
