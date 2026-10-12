using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using InvoiceAI.Ai.Configuration;
using InvoiceAI.Services.Abstractions;
namespace InvoiceAI.Ai.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(IOptions<ProviderOptions> options, IObjectStore storage) : ControllerBase
{
    [HttpGet("live")] public IActionResult Live() => Ok(new { status = "ok" });
    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        if (!options.Value.Configured)
            return StatusCode(503, new
            {
                code = "AI_CONFIGURATION_MISSING"
            });
        try
        {
            await storage.EnsureBucketAsync(ct);
            return Ok(new
            {
                status = "ready",
                mode = options.Value.Mode
            });
        }
        catch { return StatusCode(503, new { code = "STORAGE_UNAVAILABLE" }); }
    }
}
