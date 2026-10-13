namespace TutorAI.Domain.DTOs;

public sealed record TutorOutput(
    string Explanation,
    ExerciseDraft? Exercise,
    decimal? Score,
    CriterionFeedback[] Criteria,
    EvidenceCitation[] Citations
);
