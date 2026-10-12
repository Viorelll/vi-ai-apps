using Microsoft.AspNetCore.Mvc;
using VideoStudioAI.Contracts.Dto;
using VideoStudioAI.Services;
using VideoStudioAI.Services.Interfaces;

namespace VideoStudioAI.Api.Controllers;

[ApiController]
[Route("api/video/projects")]
public sealed class ProjectsController(ProjectService projects, IAiGateway ai) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await projects.ListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateProjectRequest r, CancellationToken ct)
    {
        var p = await projects.CreateAsync(r, ct);
        return Created($"/api/video/projects/{p.Id}", p);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await projects.GetAsync(id, ct));

    [HttpPost("{id:guid}/storyboard")]
    public async Task<IActionResult> Storyboard(Guid id, StoryboardRequest r, CancellationToken ct)
    {
        var j = await projects.StoryboardAsync(id, r, ct);
        return Accepted($"/api/video/projects/{id}", new { jobId = j.Id });
    }

    [HttpPatch("{id:guid}/storyboard")]
    public async Task<IActionResult> Confirm(
        Guid id,
        UpdateStoryboardRequest r,
        CancellationToken ct
    ) => Ok(await projects.ConfirmAsync(id, r, ct));

    [HttpPost("{id:guid}/generate")]
    public async Task<IActionResult> Generate(Guid id, GenerateRequest r, CancellationToken ct)
    {
        var j = await projects.GenerateAsync(id, r, ct);
        return Accepted($"/api/video/projects/{id}", new { jobId = j.Id });
    }

    [HttpGet("/api/video/capabilities")]
    public async Task<IActionResult> Capabilities(CancellationToken ct) =>
        Ok(await ai.CapabilitiesAsync(ct));

    [HttpPost("/api/video/jobs/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, RetryJobRequest r, CancellationToken ct)
    {
        var j = await projects.RetryAsync(id, r.RequestId, r.Revision, r.AcknowledgeCost, ct);
        return Accepted($"/api/video/projects/{j.ProjectId}", new { jobId = j.Id });
    }
}
