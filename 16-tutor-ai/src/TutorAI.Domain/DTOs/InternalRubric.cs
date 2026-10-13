namespace TutorAI.Domain.DTOs;

public sealed record InternalRubric(
    string ExpectedAnswer,
    string[] Criteria,
    string Version = "rubric-v1"
);
