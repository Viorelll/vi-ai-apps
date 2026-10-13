namespace TutorAI.Domain.DTOs;

public sealed record AttemptResult(Guid Id, Guid RequestId, decimal? Score, TutorOutput? Feedback);
