using InvoiceAI.Contracts.Dto;
using InvoiceAI.Database.Persistence;
using InvoiceAI.Services.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace InvoiceAI.Api.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(IDbContextFactory<AppDbContext> factory) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<JobDto> Get(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var j = await db.IngestionJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException("JOB_NOT_FOUND", 404);
        return new(j.Id, j.DocumentId, j.TargetId, j.Kind, j.Status, j.Phase, j.Attempt, j.ErrorCode);
    }
}
