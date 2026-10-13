namespace BuildBudgetAI.Domain.Dto;

public sealed record ChatRequest(Guid PlanId, Guid? ConversationId, string Question);
