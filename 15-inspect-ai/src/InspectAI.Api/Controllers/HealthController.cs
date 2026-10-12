using InspectAI.Database.Persistence;
using InspectAI.Infrastructure.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InspectAI.Api.Controllers;

[ApiController, Route("api/health")]
public sealed class HealthController(IDbContextFactory<AppDbContext> factory, IObjectStore storage)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Infrastructure(CancellationToken ct)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            if (!await db.Database.CanConnectAsync(ct))
                return StatusCode(503, new { status = "database_unavailable" });
            await storage.EnsureBucketAsync(ct);
            return Ok(new { status = "ok" });
        }
        catch
        {
            return StatusCode(503, new { status = "infrastructure_not_ready" });
        }
    }
}
