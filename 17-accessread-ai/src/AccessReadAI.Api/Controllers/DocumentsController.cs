using AccessReadAI.Contracts;
using AccessReadAI.Database;
using AccessReadAI.Infrastructure.Storage;
using AccessReadAI.Services.Documents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccessReadAI.Api.Controllers;

[ApiController, Route("api/documents")]
public sealed class DocumentsController(
    AppDbContext db,
    DocumentService documents,
    IObjectStore storage
) : ControllerBase
{
    [
        HttpPost,
        RequestSizeLimit(55 * 1024 * 1024),
        RequestFormLimits(MultipartBodyLengthLimit = 55 * 1024 * 1024)
    ]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        var result = await documents.UploadAsync(file, ct);
        var body = new
        {
            documentId = result.Document.Id,
            result.Document.State,
            existing = result.Existing,
        };
        return result.Existing ? Ok(body) : Accepted(body);
    }

    [HttpGet]
    public async Task<object> List(CancellationToken ct) =>
        await db
            .Documents.Where(x => x.State != "Deleting")
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.FileName,
                x.ContentType,
                x.State,
                x.SizeBytes,
                x.ErrorCode,
            })
            .Take(100)
            .ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<object> Get(Guid id, CancellationToken ct)
    {
        var doc =
            await db.Documents.FindAsync([id], ct) ?? throw new ApiFault(404, "document_not_found");
        if (doc.State == "Deleting")
            throw new ApiFault(410, "document_deleting");
        return new
        {
            doc.Id,
            doc.FileName,
            doc.State,
            doc.ErrorCode,
            doc.ActiveGeneration,
        };
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        var doc =
            await db.Documents.FindAsync([id], ct) ?? throw new ApiFault(404, "document_not_found");
        if (doc.State == "Deleting")
            throw new ApiFault(410, "document_deleting");
        var obj = await storage.OpenAsync(doc.ObjectKey, ct);
        Response.OnCompleted(() =>
        {
            obj.Dispose();
            return Task.CompletedTask;
        });
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(
            obj.ResponseStream,
            doc.ContentType,
            doc.FileName,
            enableRangeProcessing: false
        );
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        await documents.RetryAsync(id, ct);
        return Accepted();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await documents.DeleteAsync(id, ct);
        return Accepted();
    }
}
