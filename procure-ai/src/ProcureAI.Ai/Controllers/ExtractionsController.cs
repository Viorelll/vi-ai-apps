using Microsoft.AspNetCore.Mvc;
using ProcureAI.Ai.Providers;
using ProcureAI.Domain.DTOs;

namespace ProcureAI.Ai.Controllers;

[ApiController]
[Route("internal/extractions")]
public sealed class ExtractionsController(ExtractionProvider provider) : ControllerBase
{
    [HttpPost]
    public Task<ExtractionResult> Extract(ExtractionRequest request, CancellationToken ct) => provider.ExtractAsync(request, ct);
}
