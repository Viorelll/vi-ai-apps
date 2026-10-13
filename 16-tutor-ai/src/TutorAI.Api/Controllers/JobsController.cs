using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorAI.Database.Persistence;

namespace TutorAI.Api.Controllers;

[ApiController, Route("api/jobs")]
public sealed class JobsController(IDbContextFactory<AppDbContext> factory) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var j = await db
            .IngestionJobs.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.ErrorCode,
                x.Attempt,
                x.NextAttemptAtUtc,
            })
            .SingleOrDefaultAsync(ct);
        return j is null ? NotFound() : Ok(j);
    }
}
