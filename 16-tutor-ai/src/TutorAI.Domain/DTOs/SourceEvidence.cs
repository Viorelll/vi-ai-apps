namespace TutorAI.Domain.DTOs;

public sealed record SourceEvidence(
    Guid Id,
    string Title,
    string Objective,
    string Text,
    string OfficialUrl,
    DateTimeOffset CheckedAtUtc
);
