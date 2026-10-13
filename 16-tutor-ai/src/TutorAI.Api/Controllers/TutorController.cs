using Microsoft.AspNetCore.Mvc;
using TutorAI.Domain.DTOs;
using TutorAI.Services.Tutoring;

namespace TutorAI.Api.Controllers;

[ApiController, Route("api/tutor")]
public sealed class TutorController(TutorService service) : ControllerBase
{
    [HttpPost("sessions")]
    public async Task<IActionResult> Create(CreateSessionRequest request, CancellationToken ct)
    {
        var s = await service.CreateAsync(request, ct);
        return Created($"/api/tutor/sessions/{s.Id}", s);
    }

    [HttpGet("sessions/{id:guid}")]
    public async Task<SessionResult> Get(Guid id, CancellationToken ct) =>
        await service.GetAsync(id, ct);

    [HttpPost("sessions/{id:guid}/explain")]
    public async Task<IActionResult> Explain(Guid id, ExplainRequest r, CancellationToken ct) =>
        Accepted(
            new { jobId = await service.QueueAsync(id, "explain", r.Question, r.Revision, ct) }
        );

    [HttpPost("sessions/{id:guid}/exercises")]
    public async Task<IActionResult> Exercise(
        Guid id,
        CreateExerciseRequest r,
        CancellationToken ct
    ) => Accepted(new { jobId = await service.QueueAsync(id, "exercise", r.Kind, r.Revision, ct) });

    [HttpGet("exercises/{id:guid}")]
    public async Task<PublicExercise> Exercise(Guid id, CancellationToken ct) =>
        await service.ExerciseAsync(id, ct);

    [HttpPost("exercises/{id:guid}/attempts")]
    public async Task<IActionResult> Attempt(Guid id, AttemptRequest r, CancellationToken ct)
    {
        var a = await service.AttemptAsync(id, r, ct);
        return a.Score is null ? Accepted(a) : Ok(a);
    }

    [HttpPost("sessions/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, RetryRequest r, CancellationToken ct) =>
        Accepted(new { jobId = await service.RetryAsync(id, r.Revision, ct) });
}
