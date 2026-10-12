namespace InvoiceAI.Contracts.Dto;

public sealed record ChatResponse(Guid ConversationId, string Answer, IReadOnlyList<CitationDto> Citations, long InputTokens, long OutputTokens);
