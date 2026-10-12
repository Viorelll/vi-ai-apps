using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopVisionAI.Database;
using ShopVisionAI.Infrastructure.Storage;
using ShopVisionAI.Services.Documents;
using ShopVisionAI.Services.Errors;

namespace ShopVisionAI.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(
    DocumentService service,
    AppDbContext db,
    ObjectStore storage
) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(55L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50L * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var (document, existing) = await service.UploadAsync(
            stream,
            file.FileName,
            file.Length,
            ct
        );
        var value = new
        {
            documentId = document.Id,
            document.Status,
            document.FileName,
            document.Mime,
            document.Size,
        };
        return existing ? Ok(value) : Accepted(value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var d =
            await db
                .Documents.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.Status != "Deleted", ct)
            ?? throw new DomainException("document_not_found", 404);
        return Ok(
            new
            {
                d.Id,
                d.FileName,
                d.Mime,
                d.Size,
                d.Status,
                d.CreatedAtUtc,
            }
        );
    }

    [HttpGet("{id:guid}/content")]
    public async Task Content(Guid id, CancellationToken ct)
    {
        var d =
            await db
                .Documents.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.Status != "Deleted", ct)
            ?? throw new DomainException("document_not_found", 404);
        using var obj = await storage.OpenAsync(d.ObjectKey, ct);
        Response.ContentType = d.Mime;
        Response.ContentLength = obj.ContentLength;
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        await obj.ResponseStream.CopyToAsync(Response.Body, ct);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Accepted();
    }
}
