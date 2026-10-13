namespace TutorAI.Domain.DTOs;

public sealed record AddSourceRequest(
    Guid DocumentId,
    string OfficialUrl,
    string Objective,
    string Title
);
