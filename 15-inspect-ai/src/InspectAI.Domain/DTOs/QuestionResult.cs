namespace InspectAI.Domain.DTOs;

public sealed record QuestionResult(
    string Answer,
    ManualEvidence[] Evidence,
    Guid[] PhotoDocumentIds
);
