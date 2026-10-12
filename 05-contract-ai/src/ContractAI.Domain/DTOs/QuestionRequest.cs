namespace ContractAI.Domain.DTOs;

public sealed record QuestionRequest(Guid RequestId, string Question, Guid? ConversationId = null);
