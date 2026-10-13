namespace AccessReadAI.Contracts;

public sealed record ChatRequest(
    Guid RequestId,
    Guid? ConversationId,
    string Question,
    Guid[] DocumentIds
);
