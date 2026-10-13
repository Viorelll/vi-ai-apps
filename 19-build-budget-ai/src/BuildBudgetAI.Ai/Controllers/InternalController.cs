using BuildBudgetAI.Ai.Providers;
using BuildBudgetAI.Domain.Dto;
using Microsoft.AspNetCore.Mvc;

namespace BuildBudgetAI.Ai.Controllers;

[ApiController, Route("internal")]
public sealed class InternalController(
    FoundryProvider foundry,
    ExtractionProvider extraction,
    OfferExtractionProvider offers
) : ControllerBase
{
    [HttpPost("offers")]
    public Task<OfferDraft> Offers(OfferExtractionRequest request, CancellationToken ct) =>
        offers.ExtractAsync(request, ct);

    [HttpPost("generate")]
    public Task<GenerateResult> Generate(GenerateRequest request, CancellationToken ct) =>
        foundry.GenerateAsync(request, ct);

    [HttpPost("extract")]
    public Task<ExtractionResult> Extract(ExtractionRequest request, CancellationToken ct) =>
        extraction.ExtractAsync(request, ct);
}
