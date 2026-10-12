using ContractAI.Database.Persistence;
using ContractAI.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContractAI.Api.Controllers;

[ApiController, Route("api/jobs")]
public sealed class JobsController(IDbContextFactory<AppDbContext> factory) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var j =
            await db.IngestionJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "job_not_found", "Job not found.");
        return Ok(
            new
            {
                j.Id,
                j.Kind,
                j.Status,
                j.Attempt,
                j.ErrorCode,
                j.TargetId,
                j.DocumentId,
            }
        );
    }
}
