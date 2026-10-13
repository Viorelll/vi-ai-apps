namespace ModerateAI.Domain.Dto;

public sealed record AnalysisResult(
    SafetySnapshot Snapshot,
    string Explanation,
    long InputTokens,
    long OutputTokens
);
