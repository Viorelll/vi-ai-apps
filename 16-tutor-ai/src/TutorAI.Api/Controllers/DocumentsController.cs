using Microsoft.AspNetCore.Mvc;
using TutorAI.Domain.Models;
using TutorAI.Infrastructure.Abstractions;
using TutorAI.Services.Documents;

namespace TutorAI.Api.Controllers;

[ApiController, Route("api/documents")]
public sealed class DocumentsController(DocumentService documents, IObjectStore storage)
    : ControllerBase
{
    [HttpPost, RequestSizeLimit(55 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file.Length > FileValidator.MaxBytes)
            throw new AppException(413, "file_size_limit", "File exceeds 50 MiB.");
        await using var input = file.OpenReadStream();
        var result = await documents.UploadAsync(file.FileName, input, ct);
        return result.Existing
            ? Ok(result)
            : Accepted($"/api/documents/{result.DocumentId}", result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var d = await documents.GetAsync(id, ct);
        return Ok(
            new
            {
                d.Id,
                d.FileName,
                d.ContentType,
                d.SizeBytes,
                d.Status,
                d.ActiveGeneration,
                d.ErrorCode,
                d.CreatedAtUtc,
            }
        );
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        var d = await documents.GetAsync(id, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(
            await storage.OpenReadAsync(d.ObjectKey, ct),
            d.ContentType,
            d.FileName,
            enableRangeProcessing: false
        );
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct) =>
        Accepted(new { jobId = await documents.RetryAsync(id, ct) });

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        Accepted(new { jobId = await documents.DeleteAsync(id, ct) });
}
