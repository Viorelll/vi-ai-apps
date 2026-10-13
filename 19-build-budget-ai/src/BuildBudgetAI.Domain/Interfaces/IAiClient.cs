using BuildBudgetAI.Domain.Dto;

namespace BuildBudgetAI.Domain.Interfaces;

public interface IAiClient
{
    Task<OfferDraft> ExtractOffersAsync(OfferExtractionRequest request, CancellationToken ct);
    Task<ExtractionResult> ExtractAsync(ExtractionRequest request, CancellationToken ct);
    Task<GenerateResult> GenerateAsync(GenerateRequest request, CancellationToken ct);
}
