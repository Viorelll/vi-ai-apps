namespace InvoiceAI.Contracts.Dto;

public sealed record ChatRequest(Guid InvoiceId, string Question, Guid? ConversationId);
