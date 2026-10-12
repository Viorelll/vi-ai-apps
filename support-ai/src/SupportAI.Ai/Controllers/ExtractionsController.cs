using Microsoft.AspNetCore.Mvc;
using SupportAI.Ai.Services;
using SupportAI.Domain.DTOs;
namespace SupportAI.Ai.Controllers;

[ApiController]
[Route("internal/extractions")]
public sealed class ExtractionsController(ContentUnderstandingService service) : ControllerBase
{
    [HttpPost("start")] public Task<ExtractionResult> Start(ExtractionRequest request, CancellationToken ct) => service.StartAsync(request, ct);
    [HttpPost("poll")] public Task<ExtractionResult> Poll(ExtractionPoll request, CancellationToken ct) => service.PollAsync(request.OperationUrl, ct);
}
