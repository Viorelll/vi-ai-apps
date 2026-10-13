namespace VideoSearchAI.Contracts.Dto;

public sealed record GeneratedAnswer(
    string Answer,
    GeneratedMatch[] Matches,
    long InputTokens,
    long OutputTokens,
    string Deployment
);
