namespace EvalAI.Domain.DTOs;

public sealed record ChatAnswer(Guid ConversationId, string Answer, EvidenceSource[] Citations, TokenUsage Usage);
