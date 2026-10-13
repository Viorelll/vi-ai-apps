namespace TutorAI.Domain.DTOs;

public sealed record SessionResult(
    Guid Id,
    string Topic,
    int Difficulty,
    string State,
    long Revision,
    TutorOutput? Explanation,
    PublicExercise? Exercise,
    AttemptResult? Feedback,
    string? ErrorCode,
    bool DemoMode
);
