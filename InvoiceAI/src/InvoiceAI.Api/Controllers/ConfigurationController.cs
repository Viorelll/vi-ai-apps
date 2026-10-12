using Microsoft.AspNetCore.Mvc;
namespace InvoiceAI.Api.Controllers;

[ApiController]
[Route("api/config")]
public sealed class ConfigurationController(IConfiguration configuration) : ControllerBase
{
    [HttpGet] public object Get() => new { mode = configuration["Ai:Mode"] ?? "Demo", ragProvider = "NoRag", maxFileBytes = 50L * 1024 * 1024 };
}
