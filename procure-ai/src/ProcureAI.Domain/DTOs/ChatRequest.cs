namespace ProcureAI.Domain.DTOs;

public sealed record ChatRequest(Guid RunId, string Question, Guid? ConversationId);
