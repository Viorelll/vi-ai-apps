using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoSearchAI.Api.Results;
using VideoSearchAI.Database;
using VideoSearchAI.Domain.Models;
using VideoSearchAI.Services.Services;

namespace VideoSearchAI.Api.Controllers;

[ApiController, Route("api/documents")]
public sealed class DocumentsController(
    UploadService upload,
    LibraryService library,
    IDbContextFactory<AppDbContext> factory
) : ControllerBase
{
    [HttpPost, RequestSizeLimit(52494336), RequestFormLimits(MultipartBodyLengthLimit = 52428800)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        var dto = await upload.UploadAsync(file, ct);
        return dto.Duplicate ? Ok(dto) : Accepted($"/api/documents/{dto.DocumentId}", dto);
    }

    [HttpGet("{id:guid}")]
    public async Task<object> Get(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var d =
            await db.Documents.SingleOrDefaultAsync(x => x.Id == id && !x.Deleted, ct)
            ?? throw new DomainException("document_not_found", 404);
        return new
        {
            documentId = d.Id,
            d.FileName,
            d.ContentType,
            d.SizeBytes,
            d.DurationMs,
            d.Status,
            d.ActiveGeneration,
        };
    }

    [HttpGet("{id:guid}/content"), HttpHead("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var d =
            await db.Documents.SingleOrDefaultAsync(x => x.Id == id && !x.Deleted, ct)
            ?? throw new DomainException("document_not_found", 404);
        return new VideoContentResult(d);
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var jobId = await library.RetryDocumentAsync(id, ct);
        return Accepted($"/api/jobs/{jobId}", new { jobId });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await library.DeleteAsync(id, ct);
        return Accepted();
    }
}
