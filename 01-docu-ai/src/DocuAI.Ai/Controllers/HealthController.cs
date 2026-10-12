using Microsoft.AspNetCore.Mvc;

namespace DocuAI.Ai.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("health/live")]
    public IActionResult Live() => Ok(new { status = "ok" });
}
