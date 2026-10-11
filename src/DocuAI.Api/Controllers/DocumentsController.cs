using DocuAI.Application.Services;
using DocuAI.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace DocuAI.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(DocumentService documents) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await documents.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await documents.GetAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(55L * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null) return BadRequest(new { error = "Choose a file to upload." });

        try
        {
            var result = await documents.UploadAsync(file, cancellationToken);
            if (result.Deduplicated) return Ok(new { documentId = result.Document.Id, status = result.Document.Status, deduplicated = true });
            return AcceptedAtAction(nameof(Get), new { id = result.Document.Id }, new { documentId = result.Document.Id, status = result.Document.Status });
        }
        catch (DocumentValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (DocumentTooLargeException)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken cancellationToken)
    {
        var result = await documents.OpenContentAsync(id, cancellationToken);
        return result is null
            ? NotFound()
            : File(result.Stream, result.ContentType, result.FileName, enableRangeProcessing: true);
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return await documents.RetryAsync(id, cancellationToken)
                ? Accepted($"/api/documents/{id}")
                : NotFound();
        }
        catch (DocumentConflictException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await documents.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
}
