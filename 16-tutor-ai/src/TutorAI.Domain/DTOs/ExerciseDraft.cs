namespace TutorAI.Domain.DTOs;

public sealed record ExerciseDraft(
    string Kind,
    string Prompt,
    string[] Options,
    InternalRubric Rubric
);
