namespace DocuAI.Domain.DTOs;

public sealed record AskRequest(
    string Question,
    Guid? ConversationId,
    IReadOnlyList<Guid>? DocumentIds);
