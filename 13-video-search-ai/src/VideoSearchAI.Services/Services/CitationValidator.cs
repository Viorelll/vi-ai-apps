using VideoSearchAI.Contracts.Dto;
using VideoSearchAI.Domain.Models;
using VideoSearchAI.Domain.Services;

namespace VideoSearchAI.Services.Services;

public static class CitationValidator
{
    public static CitationDto[] Validate(
        GeneratedAnswer answer,
        GroundingSource[] sources,
        IReadOnlyDictionary<Guid, long> durations
    )
    {
        if (
            string.IsNullOrWhiteSpace(answer.Answer)
            || answer.Answer.Length > 12000
            || answer.Matches.Length > 8
            || answer.InputTokens < 0
            || answer.OutputTokens < 0
        )
            throw new DomainException("invalid_ai_response", 502);
        return answer
            .Matches.Select(m =>
            {
                var source =
                    sources.SingleOrDefault(x =>
                        x.Label == m.CitationLabel
                        && x.Segment.Id == m.SegmentId
                        && x.Segment.VideoId == m.VideoId
                    ) ?? throw new DomainException("invalid_citation", 502);
                var s = source.Segment;
                TemporalRules.ToOriginalRange(s.StartMs, s.EndMs, 0, durations[s.VideoId]);
                if (m.EvidenceKind is not "transcript" and not "visual")
                    throw new DomainException("invalid_evidence_kind", 502);
                var excerpt = m.EvidenceKind == "visual" ? s.VisualText : s.Transcript;
                if (string.IsNullOrWhiteSpace(excerpt))
                    throw new DomainException("missing_evidence", 502);
                return new CitationDto(
                    source.Label,
                    s.VideoId,
                    s.Id,
                    s.DocumentId,
                    s.Generation,
                    s.StartMs,
                    s.EndMs,
                    excerpt,
                    $"/api/video/library/{s.VideoId}/content",
                    m.EvidenceKind
                );
            })
            .DistinctBy(x => x.Label)
            .ToArray();
    }
}
