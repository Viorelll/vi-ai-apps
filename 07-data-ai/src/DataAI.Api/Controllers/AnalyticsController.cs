using DataAI.Application.Abstractions;
using DataAI.Domain.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace DataAI.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class AnalyticsController(IDataAIService service) : ControllerBase
{
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze(AnalyzeRequest request, CancellationToken ct)
    {
        try { return Ok(await service.AnalyzeAsync(request, ct)); }
        catch (KeyNotFoundException) { return NotFound(new { code = "dataset_not_found" }); }
        catch (ArgumentException ex) { return BadRequest(new { code = ex.Message }); }
        catch (HttpRequestException) { return Problem("AI provider is unavailable.", statusCode: 502, extensions: new Dictionary<string, object?> { ["code"] = "ai_provider_unavailable" }); }
    }

    [HttpGet("runs/{id:guid}")]
    public async Task<IActionResult> GetRun(Guid id, CancellationToken ct) => await service.GetRunAsync(id, ct) is { } run ? Ok(run) : NotFound();
}
