using Microsoft.AspNetCore.Mvc;
using InvoiceAI.Ai.Providers.Foundry;
using InvoiceAI.Contracts.Dto;
namespace InvoiceAI.Ai.Controllers;

[ApiController]
[Route("internal/domain")]
public sealed class DomainController(FoundryExplanationProvider provider) : ControllerBase
{
    [HttpPost("step")] public Task<ExplanationDto> Explain(ExplainRequest request, CancellationToken ct) => provider.ExplainAsync(request, ct);
}
