namespace InspectAI.Domain.DTOs;

public sealed record FindingCandidate(
    Guid Id,
    Guid DocumentId,
    string Kind,
    string Description,
    string? Reason,
    Region? Region
);
