namespace InspectAI.Domain.DTOs;

public sealed record ChecklistCandidate(
    Guid Id,
    Guid FindingId,
    string Text,
    ManualEvidence[] Evidence
);
