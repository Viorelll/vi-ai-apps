using Microsoft.AspNetCore.Mvc;
using ModerateAI.Domain.Interfaces;
using ModerateAI.Services;

namespace ModerateAI.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(DocumentService service, IObjectStore store)
    : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await service.UploadAsync(stream, file.FileName, ct);
        var dto = new
        {
            result.Document.Id,
            result.Document.FileName,
            result.Document.ContentType,
            result.Document.SizeBytes,
            result.Document.Sha256,
            result.Document.Status,
        };
        return result.Existing ? Ok(dto) : Created($"/api/documents/{result.Document.Id}", dto);
    }

    [HttpGet("{id:guid}")]
    public async Task<object> Get(Guid id, CancellationToken ct)
    {
        var doc = await service.GetAsync(id, ct);
        return new
        {
            doc.Id,
            doc.FileName,
            doc.ContentType,
            doc.SizeBytes,
            doc.Sha256,
            doc.Status,
            doc.CreatedAtUtc,
        };
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        var doc = await service.GetAsync(id, ct);
        return File(
            await store.ReadAsync(doc.ObjectKey, ct),
            doc.ContentType,
            doc.FileName,
            enableRangeProcessing: true
        );
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Accepted(new { documentId = id, status = "Deleting" });
    }
}
