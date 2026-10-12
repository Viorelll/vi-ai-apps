using MeetingAI.Domain.DTOs;
namespace MeetingAI.Api.Services;

public static class SegmentValidator
{
    public static void Validate(IEnumerable<SegmentDto> segments, long audioDurationMs)
    {
        var values = segments.ToArray();
        if (values.Any(s => s.StartMs < 0 || s.EndMs <= s.StartMs || s.EndMs > audioDurationMs || string.IsNullOrWhiteSpace(s.Text))) throw new ArgumentException("invalid_segment");
        if (values.Select(s => s.Id).Distinct().Count() != values.Length) throw new ArgumentException("duplicate_segment_id");
    }
}
