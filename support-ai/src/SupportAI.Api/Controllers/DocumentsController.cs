using Microsoft.AspNetCore.Mvc;
using SupportAI.Application.Abstractions;
using SupportAI.Application.Services;
using SupportAI.Domain.Entities;
namespace SupportAI.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(DocumentService documents, IObjectStore storage) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<Document>> List(CancellationToken ct) => documents.ListAsync(ct);
    [HttpGet("{id:guid}")] public Task<Document> Get(Guid id, CancellationToken ct) => documents.GetAsync(id, ct);
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(55L * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await documents.UploadAsync(file.FileName, stream, ct);
        return result.Deduplicated ? Ok(result) : AcceptedAtAction(nameof(Get), new
        {
            id = result.DocumentId
        }, result);
    }
    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        var doc = await documents.GetAsync(id, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Accept-Ranges"] = "bytes";
        if (Request.Headers.TryGetValue("Range", out var value))
        {
            if (!System.Net.Http.Headers.RangeHeaderValue.TryParse(value.ToString(), out var range) || range.Unit != "bytes" || range.Ranges.Count != 1)
                return StatusCode(416);
            var requested = range.Ranges.Single();
            var start = requested.From ?? Math.Max(0, doc.SizeBytes - (requested.To ?? 0));
            var end = requested.From is null ? doc.SizeBytes - 1 : Math.Min(requested.To ?? doc.SizeBytes - 1, doc.SizeBytes - 1);
            if (start < 0 || start >= doc.SizeBytes || end < start)
            {
                Response.Headers["Content-Range"] = $"bytes */{doc.SizeBytes}";
                return StatusCode(416);
            }
            Response.StatusCode = 206;
            Response.Headers["Content-Range"] = $"bytes {start}-{end}/{doc.SizeBytes}";
            Response.ContentLength = end - start + 1;
            return File(await storage.OpenRangeAsync(doc.ObjectKey, start, end, ct), doc.ContentType, doc.FileName);
        }
        Response.ContentLength = doc.SizeBytes;
        return File(await storage.OpenAsync(doc.ObjectKey, ct), doc.ContentType, doc.FileName);
    }
    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        await documents.RetryAsync(id, ct);
        return Accepted($"/api/documents/{id}");
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await documents.DeleteAsync(id, ct);
        return Accepted();
    }
}
