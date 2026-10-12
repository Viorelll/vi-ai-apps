using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProcureAI.Application.Abstractions;
using ProcureAI.Database.Persistence;

namespace ProcureAI.Api.Controllers;

[ApiController]
public sealed class HealthController(IDbContextFactory<AppDbContext> factory, IObjectStore objects,
    IHttpClientFactory clients, IConfiguration configuration) : ControllerBase
{
    [HttpGet("health/live")]
    public IActionResult Live() => Ok(new { status = "ok" });
    [HttpGet("health/ready")]
    [HttpGet("api/health")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await db.Database.CanConnectAsync(ct)) return StatusCode(503);
        await objects.EnsureBucketAsync(ct);
        using var ai = await clients.CreateClient("health-ai").GetAsync("health/ready", ct);
        return ai.IsSuccessStatusCode ? Ok(new { status = "ready", mode = configuration["Ai:Mode"] ?? "Foundry" }) : StatusCode(503);
    }
}
