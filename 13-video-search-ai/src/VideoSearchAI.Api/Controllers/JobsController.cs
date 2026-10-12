using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoSearchAI.Contracts.Dto;
using VideoSearchAI.Database;
using VideoSearchAI.Domain.Models;

namespace VideoSearchAI.Api.Controllers;

[ApiController, Route("api/jobs")]
public sealed class JobsController(IDbContextFactory<AppDbContext> factory) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<JobDto> Get(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var j =
            await db.Jobs.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("job_not_found", 404);
        return new(j.Id, j.Status, j.Step, j.Attempt, j.ErrorCode);
    }
}
