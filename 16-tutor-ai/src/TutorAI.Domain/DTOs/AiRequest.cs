namespace TutorAI.Domain.DTOs;

public sealed record AiRequest(
    Guid OperationId,
    string Step,
    string Topic,
    int Difficulty,
    string Question,
    SourceEvidence[] Sources,
    InternalRubric? Rubric = null,
    string? Answer = null,
    string InputHash = "",
    string PromptVersion = "tutor-v1"
);
