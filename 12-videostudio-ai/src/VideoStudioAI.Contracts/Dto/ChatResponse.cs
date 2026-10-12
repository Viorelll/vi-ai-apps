namespace VideoStudioAI.Contracts.Dto;

public sealed record ChatResponse(
    Guid ConversationId,
    string Answer,
    CitationDto[] Citations,
    long InputTokens,
    long OutputTokens
);
