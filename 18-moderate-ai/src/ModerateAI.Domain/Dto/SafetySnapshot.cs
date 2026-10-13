namespace ModerateAI.Domain.Dto;

public sealed record SafetySnapshot(
    string Provider,
    string ApiVersion,
    string OutputScale,
    IReadOnlyList<SafetyScore> Scores,
    bool Complete,
    string RawJson
);
