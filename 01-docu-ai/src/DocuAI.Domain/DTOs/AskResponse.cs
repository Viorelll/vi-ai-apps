namespace DocuAI.Domain.DTOs;

public sealed record AskResponse(
    Guid ConversationId,
    string Answer,
    IReadOnlyList<CitationDto> Citations,
    long InputTokens,
    long OutputTokens,
    long DurationMs);
