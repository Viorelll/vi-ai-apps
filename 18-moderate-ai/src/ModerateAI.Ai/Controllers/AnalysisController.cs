using Microsoft.AspNetCore.Mvc;
using ModerateAI.Ai.Services;
using ModerateAI.Domain.Dto;

namespace ModerateAI.Ai.Controllers;

[ApiController]
[Route("internal/moderation")]
public sealed class AnalysisController(AnalysisService service) : ControllerBase
{
    [HttpPost("analyze")]
    public async Task<AnalysisResult> Analyze(AnalysisRequest request, CancellationToken ct) =>
        await service.AnalyzeAsync(request, ct);
}
