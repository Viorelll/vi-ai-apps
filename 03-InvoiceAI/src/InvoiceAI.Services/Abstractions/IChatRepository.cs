using InvoiceAI.Contracts.Dto;
namespace InvoiceAI.Services.Abstractions;

public interface IChatRepository
{
    Task<(Guid Id, IReadOnlyList<ChatTurnDto> History)> GetHistoryAsync(Guid invoiceId, Guid? conversationId, CancellationToken ct);
    Task SaveAsync(Guid conversationId, string question, ExplanationDto answer, IReadOnlyList<CitationDto> citations, int generation, CancellationToken ct);
}
