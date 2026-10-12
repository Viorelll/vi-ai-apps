namespace VideoStudioAI.Contracts.Dto;

public sealed record GroundedAnswer(
    string Answer,
    Guid[] SourceIds,
    long InputTokens = 0,
    long OutputTokens = 0
);
