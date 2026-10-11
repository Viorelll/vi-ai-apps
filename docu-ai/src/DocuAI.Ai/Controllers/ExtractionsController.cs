using System.Security.Cryptography;
using System.Text;
using DocuAI.Ai.Services;
using DocuAI.Ai.Services.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DocuAI.Ai.Controllers;

[ApiController]
[Route("internal/extractions")]
public sealed class ExtractionsController(
    ContentUnderstandingService extraction,
    IOptions<InternalApiOptions> internalOptions) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(55L * 1024 * 1024)]
    public async Task<IActionResult> Extract([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (!HasValidInternalKey()) return Unauthorized();
        if (file is null || file.Length == 0) return BadRequest(new { error = "Missing file." });

        try
        {
            return Ok(new { text = await extraction.ExtractAsync(file, cancellationToken) });
        }
        catch (ContentUnderstandingConfigurationException)
        {
            return Problem("Content Understanding is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (ContentUnderstandingRequestException)
        {
            return Problem("Content Understanding could not extract this file.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private bool HasValidInternalKey()
    {
        if (!Request.Headers.TryGetValue("X-Internal-Key", out var value)) return false;
        var expected = Encoding.UTF8.GetBytes(internalOptions.Value.Key);
        var supplied = Encoding.UTF8.GetBytes(value.ToString());
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}
