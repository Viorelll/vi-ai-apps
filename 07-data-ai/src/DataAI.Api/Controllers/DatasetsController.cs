using DataAI.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace DataAI.Api.Controllers;

[ApiController]
[Route("api/datasets")]
public sealed class DatasetsController(IDataAIService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.GetDatasetsAsync(ct));

    [HttpPost("import")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Import([FromForm] IFormFile file, [FromForm] string? name, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { code = "csv_required" });
        await using var stream = file.OpenReadStream();
        return Ok(await service.ImportCsvAsync(string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(file.FileName) : name, stream, ct));
    }
}
