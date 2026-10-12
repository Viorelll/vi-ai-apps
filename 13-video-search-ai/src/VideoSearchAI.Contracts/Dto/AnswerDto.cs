namespace VideoSearchAI.Contracts.Dto;

public sealed record AnswerDto(
    string Answer,
    CitationDto[] Citations,
    long InputTokens,
    long OutputTokens,
    string Deployment
);
