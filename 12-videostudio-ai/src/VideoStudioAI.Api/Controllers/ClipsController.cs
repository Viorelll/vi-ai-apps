using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoStudioAI.Contracts.Dto;
using VideoStudioAI.Database;
using VideoStudioAI.Services;
using VideoStudioAI.Services.Interfaces;
using VideoStudioAI.Services.Models;

namespace VideoStudioAI.Api.Controllers;

[ApiController]
[Route("api/video/clips")]
public sealed class ClipsController(ProjectService projects, AppDbContext db, IObjectStore store)
    : ControllerBase
{
    [HttpPost("{id:guid}/remix")]
    public async Task<IActionResult> Remix(Guid id, RemixRequest r, CancellationToken ct)
    {
        var j = await projects.RemixAsync(id, r, ct);
        return Accepted($"/api/video/projects/{j.ProjectId}", new { jobId = j.Id });
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        var clip =
            await db.Clips.FindAsync([id], ct)
            ?? throw new AppException(404, "clip_missing", "Clip not found.");
        var content = await store.DownloadAsync(clip.ObjectKey, ct);
        Response.RegisterForDisposeAsync(content);
        Response.Headers.ETag = $"\"{clip.Sha256}\"";
        return File(
            System.IO.File.OpenRead(content.Path),
            "video/mp4",
            enableRangeProcessing: true
        );
    }
}
