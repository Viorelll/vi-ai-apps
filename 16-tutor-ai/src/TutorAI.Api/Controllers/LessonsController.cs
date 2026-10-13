using Microsoft.AspNetCore.Mvc;
using TutorAI.Domain.DTOs;
using TutorAI.Services.Lessons;

namespace TutorAI.Api.Controllers;

[ApiController, Route("api/lessons")]
public sealed class LessonsController(LessonService service) : ControllerBase
{
    [HttpGet("sources")]
    public Task<SourceEvidence[]> Sources([FromQuery] string? topic, CancellationToken ct) =>
        service.SourcesAsync(topic, ct);

    [HttpPost("sources")]
    public async Task<IActionResult> Add(AddSourceRequest r, CancellationToken ct)
    {
        var id = await service.AddAsync(r, ct);
        return Created($"/api/lessons/sources?id={id}", new { id });
    }
}
