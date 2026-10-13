namespace AccessReadAI.Contracts;

public sealed record GroundedAnswer(
    string Answer,
    string[] Labels,
    long InputTokens = 0,
    long OutputTokens = 0
);
