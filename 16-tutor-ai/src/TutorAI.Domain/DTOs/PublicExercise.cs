namespace TutorAI.Domain.DTOs;

public sealed record PublicExercise(
    Guid Id,
    string Kind,
    string Prompt,
    string[] Options,
    int Difficulty
);
