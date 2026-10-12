using InvoiceAI.Database.Persistence;
using InvoiceAI.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace InvoiceAI.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(IDbContextFactory<AppDbContext> factory, IObjectStore storage, IAiGateway ai) : ControllerBase
{
    [HttpGet("live")] public IActionResult Live() => Ok(new { status = "ok" });
    [HttpGet("ready")]
    [HttpGet("/api/health/ready")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            if (!await db.Database.CanConnectAsync(ct) || (await db.Database.GetPendingMigrationsAsync(ct)).Any())
                return StatusCode(503, new
                {
                    status = "database_unavailable"
                });
            await storage.EnsureBucketAsync(ct);
            if (!await ai.ReadyAsync(ct))
                return StatusCode(503, new
                {
                    status = "ai_unavailable"
                });
            return Ok(new
            {
                status = "ready"
            });
        }
        catch { return StatusCode(503, new { status = "dependency_unavailable" }); }
    }
}
