using InspectAI.Domain.Models;
using InspectAI.Infrastructure.Abstractions;
using InspectAI.Services.Documents;
using Microsoft.AspNetCore.Mvc;

namespace InspectAI.Api.Controllers;

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

    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct)
    {
        var d = await documents.GetAsync(id, ct);
        if (d.PhotoObjectKey is null)
            throw new AppException(404, "preview_not_found", "No normalized photo preview.");
        return File(await storage.OpenReadAsync(d.PhotoObjectKey, ct), "image/png");
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct) =>
        Accepted(new { jobId = await documents.RetryAsync(id, ct) });

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        Accepted(new { jobId = await documents.DeleteAsync(id, ct) });
}
