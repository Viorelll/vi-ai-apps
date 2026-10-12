using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportAI.Application.Abstractions;
using SupportAI.Database.Persistence;
namespace SupportAI.Api.Controllers;

[ApiController]
public sealed class HealthController(IDbContextFactory<AppDbContext> factory, IObjectStore storage, IConfiguration config, IHttpClientFactory clients) : ControllerBase
{
    [HttpGet("health/live")] public IActionResult Live() => Ok(new { status = "ok" });
    [HttpGet("health/ready")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            if (!await db.Database.CanConnectAsync(ct))
                return StatusCode(503);
            if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
                return StatusCode(503);
            if (!await db.Documents.AnyAsync(x => x.IsPolicy && x.Status == "Ready", ct))
                return StatusCode(503);
            await storage.EnsureBucketAsync(ct);
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(config["Ai:BaseUrl"] ?? "http://localhost:8081/"), "health/ready"));
            request.Headers.Add("X-Internal-Key", config["Internal:Key"]);
            using var response = await clients.CreateClient("health").SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return StatusCode(503);
            return Ok(new
            {
                status = "ready"
            });
        }
        catch { return StatusCode(503, new { code = "dependency_unavailable" }); }
    }
    [HttpGet("api/config")] public IActionResult Config() => Ok(new { aiMode = config["Foundry:Mode"] ?? "Foundry", ragProvider = "NoRag", maxFileBytes = 50L * 1024 * 1024 });
}
