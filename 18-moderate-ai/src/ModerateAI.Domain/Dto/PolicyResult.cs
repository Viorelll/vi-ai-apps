namespace ModerateAI.Domain.Dto;

public sealed record PolicyResult(
    string Decision,
    string PolicyVersion,
    IReadOnlyList<string> Reasons,
    string DecisionHash
);
