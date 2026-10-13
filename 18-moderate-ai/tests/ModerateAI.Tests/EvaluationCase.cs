using ModerateAI.Domain.Dto;

namespace ModerateAI.Tests;

public sealed record EvaluationCase(
    string Name,
    string Scale,
    bool Complete,
    SafetyScore[] Scores,
    string Expected
);
