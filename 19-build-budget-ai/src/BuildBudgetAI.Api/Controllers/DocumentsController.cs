using BuildBudgetAI.Api.Services;
using BuildBudgetAI.Database;
using BuildBudgetAI.Domain.Interfaces;
using BuildBudgetAI.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BuildBudgetAI.Api.Controllers;

[ApiController, Route("api/documents")]
public sealed class DocumentsController(
    AppDbContext db,
    DocumentService documents,
    IObjectStore storage
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(
            await db
                .Documents.Where(x => x.Status != "Deleted" && x.Status != "Deleting")
                .Select(x => new
                {
                    x.Id,
                    x.FileName,
                    x.ContentType,
                    x.Status,
                    x.SizeBytes,
                    x.ErrorCode,
                })
                .ToListAsync(ct)
        );

    [
        HttpPost,
        RequestSizeLimit(53 * 1024 * 1024),
        RequestFormLimits(MultipartBodyLengthLimit = 53 * 1024 * 1024)
    ]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        var (d, existing) = await documents.UploadAsync(file, ct);
        var body = new { documentId = d.Id, status = d.Status };
        return existing ? Ok(body) : Accepted($"/api/documents/{d.Id}", body);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var d = await db.Documents.FindAsync([id], ct);
        return d == null || d.Status is "Deleting" or "Deleted"
            ? NotFound()
            : Ok(
                new
                {
                    d.Id,
                    d.FileName,
                    d.Status,
                    d.ErrorCode,
                }
            );
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        var d =
            await db.Documents.FindAsync([id], ct)
            ?? throw new DomainException("document_not_found", 404);
        if (d.Status is "Deleting" or "Deleted")
            return NotFound();
        return File(await storage.ReadAsync(d.ObjectKey, ct), d.ContentType, d.FileName);
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
