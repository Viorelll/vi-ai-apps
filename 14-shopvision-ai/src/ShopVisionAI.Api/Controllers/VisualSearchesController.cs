using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ShopVisionAI.Contracts.Dto;
using ShopVisionAI.Services.Searches;

namespace ShopVisionAI.Api.Controllers;

[ApiController]
[Route("api/visual/searches")]
public sealed class VisualSearchesController(VisualSearchService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateSearchRequest request, CancellationToken ct)
    {
        var s = await service.CreateAsync(request, ct);
        return Created(
            $"/api/visual/searches/{s.Id}",
            new
            {
                s.Id,
                s.State,
                s.Revision,
                s.CatalogVersion,
            }
        );
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var s = await service.FindAsync(id, ct);
        return Ok(
            new
            {
                s.Id,
                s.State,
                s.Revision,
                s.CatalogVersion,
                s.ErrorCode,
                s.InputTokens,
                s.OutputTokens,
                result = s.ResultJson == null
                    ? null
                    : JsonSerializer.Deserialize<SearchResult>(
                        s.ResultJson,
                        VisualSearchService.Json
                    ),
                imageUrl = $"/api/documents/{s.ImageDocumentId}/content",
            }
        );
    }

    [HttpPost("{id:guid}/analyze")]
    public async Task<IActionResult> Analyze(Guid id, AnalyzeRequest request, CancellationToken ct)
    {
        var job = await service.AnalyzeAsync(id, request, ct);
        return Accepted(new { jobId = job.Id, job.Status });
    }

    [HttpPost("{id:guid}/questions")]
    public async Task<IActionResult> Question(
        Guid id,
        QuestionRequest request,
        CancellationToken ct
    ) => Ok(await service.QuestionAsync(id, request, ct));
}
