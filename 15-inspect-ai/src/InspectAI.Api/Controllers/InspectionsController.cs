using InspectAI.Domain.DTOs;
using InspectAI.Services.Inspections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InspectAI.Api.Controllers;

[ApiController, Route("api/inspections")]
public sealed class InspectionsController(InspectionService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateInspectionRequest r, CancellationToken ct)
    {
        var i = await service.CreateAsync(r, ct);
        return Created($"/api/inspections/{i.Id}", i);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpPost("{id:guid}/sources")]
    public async Task<IActionResult> AddSource(Guid id, AddSourceRequest r, CancellationToken ct)
    {
        await service.AddSourceAsync(id, r, ct);
        return Ok(await service.GetAsync(id, ct));
    }

    [HttpPost("{id:guid}/analyze"), EnableRateLimiting("ai")]
    public async Task<IActionResult> Analyze(Guid id, AnalyzeRequest r, CancellationToken ct) =>
        Accepted(new { jobId = await service.AnalyzeAsync(id, r, ct) });

    [HttpPost("{id:guid}/questions"), EnableRateLimiting("ai")]
    public async Task<IActionResult> Question(Guid id, QuestionRequest r, CancellationToken ct) =>
        Ok(await service.QuestionAsync(id, r, ct));

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct) =>
        Ok(await service.ExportAsync(id, ct));

    [HttpPatch("{id:guid}/checklist/{itemId:guid}")]
    public async Task<IActionResult> Note(
        Guid id,
        Guid itemId,
        ChecklistStatusRequest r,
        CancellationToken ct
    )
    {
        await service.NoteAsync(id, itemId, r, ct);
        return Ok(await service.GetAsync(id, ct));
    }
}
