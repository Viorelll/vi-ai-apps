using MeetingAI.Domain.DTOs;
using MeetingAI.Domain.Entities;
namespace MeetingAI.Api.Services;

public static class MeetingMapper
{
    public static MeetingDto ToDto(Meeting m) => new(m.Id, m.Title, m.State, m.Summary, m.Revision, m.TranscriptRevision, m.CreatedAtUtc,
        m.Segments.OrderBy(s => s.Ordinal).Select(s => new SegmentDto(s.Id, s.Ordinal, s.StartMs, s.EndMs, s.SpeakerId, s.SpeakerLabel ?? s.SpeakerId, s.Text)).ToArray(),
        m.Decisions.Select(d => new DecisionDto(d.Id, d.Text, d.SegmentIds)).ToArray(),
        m.ActionItems.Select(a => new ActionItemDto(a.Id, a.Text, a.AssigneeLabel, a.DueText, a.SegmentIds)).ToArray());
}
