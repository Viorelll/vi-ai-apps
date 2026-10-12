using Microsoft.AspNetCore.Mvc;
using ProcureAI.Application.Abstractions;
using ProcureAI.Application.Exceptions;
using ProcureAI.Application.Services;
using ProcureAI.Domain.DTOs;

namespace ProcureAI.Api.Controllers;

[ApiController]
[Route("api/procurement/runs")]
public sealed class RunsController(IProcurementRepository repository, RunService runs) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await repository.ListRunsAsync(ct));
    [HttpPost]
    public async Task<IActionResult> Create(CreateRunRequest request, CancellationToken ct)
    {
        var run = await runs.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = run.Id }, new { run.Id, run.Revision });
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        await repository.GetRunAsync(id, ct) is { } run ? Ok(run) : NotFound();
    [HttpPost("{id:guid}/documents")]
    public async Task<IActionResult> Attach(Guid id, AttachDocumentRequest request, CancellationToken ct)
    { await repository.AttachAsync(id, request, ct); return Ok(await repository.GetRunAsync(id, ct)); }
    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, RevisionRequest request, CancellationToken ct) =>
        AcceptedAtAction(nameof(Get), new { id }, new { jobId = await repository.StartAsync(id, request.Revision, false, ct) });
    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, RevisionRequest request, CancellationToken ct) =>
        AcceptedAtAction(nameof(Get), new { id }, new { jobId = await repository.StartAsync(id, request.Revision, true, ct) });
    [HttpGet("{id:guid}/report")]
    public async Task<IActionResult> Report(Guid id, CancellationToken ct)
    {
        var run = await repository.GetRunAsync(id, ct) ?? throw new AppException("run_not_found", 404);
        if (run.State != "Completed" || run.Stale) throw new AppException("report_unavailable", 409);
        return Ok(run);
    }
}
