namespace ContractAI.Domain.DTOs;

public sealed record ChatRequest(
    Guid VersionId,
    Guid RequestId,
    string Question,
    Guid? ConversationId = null
);
