namespace VideoStudioAI.Contracts.Dto;

public sealed record ChatRequest(string Question, Guid[] DocumentIds, Guid? ConversationId = null);
