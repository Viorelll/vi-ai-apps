namespace ProcureAI.Domain.DTOs;

public sealed record RecommendationDto(Guid RecommendedOfferId, string SnapshotHash,
    IReadOnlyList<ReasonDto> Reasons, IReadOnlyList<string> Unknowns);
