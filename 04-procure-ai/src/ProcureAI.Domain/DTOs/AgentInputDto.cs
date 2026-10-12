namespace ProcureAI.Domain.DTOs;

public sealed record AgentInputDto(string Brief, CriteriaDto Criteria, IReadOnlyList<SourceDto> Sources,
    IReadOnlyList<OfferDto> Offers, IReadOnlyList<OfferScoreDto> Scores, string SnapshotHash, string? Question = null);
