using DocuAI.Application.Abstractions;
using DocuAI.Database.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocuAI.Api.Controllers;

[ApiController]
public sealed class HealthController(IDbContextFactory<DocuAiDbContext> dbFactory, IObjectStore objectStore) : ControllerBase
{
    [HttpGet("health/live")]
    public IActionResult Live() => Ok(new { status = "ok" });

    [HttpGet("health/ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            if (!await db.Database.CanConnectAsync(cancellationToken))
                return Problem("The database is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);

            await objectStore.EnsureBucketAsync(cancellationToken);
            return Ok(new { status = "ready" });
        }
        catch
        {
            return Problem("A required database or object store is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
