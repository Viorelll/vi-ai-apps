namespace ProcureAI.Domain.DTOs;

public sealed record ChatResultDto(Guid ConversationId, string Answer, IReadOnlyList<SourceDto> Citations,
    long InputTokens, long OutputTokens, long DurationMs);
