namespace ProcureAI.Application.Models;

using ProcureAI.Domain.DTOs;

public sealed record RunViewDto(Guid Id, string Brief, CriteriaDto Criteria, string State, long Revision,
    bool Stale, string? ErrorCode, string SnapshotHash, long BudgetTokens, long UsedTokens,
    IReadOnlyList<DocumentDto> Documents, IReadOnlyList<StepViewDto> Steps, IReadOnlyList<OfferDto> Offers,
    IReadOnlyList<OfferScoreDto> Scores, RecommendationDto? Recommendation, IReadOnlyList<SourceDto> Sources);
