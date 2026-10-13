namespace EvalAI.Domain.DTOs;

public sealed record ChatRequest(Guid RequestId, string Question, Guid[] DocumentIds, Guid? ConversationId = null);
