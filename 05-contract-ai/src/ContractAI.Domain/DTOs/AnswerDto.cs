namespace ContractAI.Domain.DTOs;

public sealed record AnswerDto(
    Guid ConversationId,
    Guid MessageId,
    string Answer,
    CitationDto[] Citations,
    string Deployment,
    long InputTokens,
    long OutputTokens
);
