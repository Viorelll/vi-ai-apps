using Microsoft.AspNetCore.Mvc;
using VideoStudioAI.Services;
using VideoStudioAI.Services.Interfaces;

namespace VideoStudioAI.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(DocumentService documents, IObjectStore store)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await documents.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(DocumentService.Dto(await documents.FindAsync(id, ct)));

    [HttpPost]
    [RequestSizeLimit(52432896)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52428800)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        await using var input = file.OpenReadStream();
        var result = await documents.UploadAsync(input, file.FileName, ct);
        return result.Existing
            ? Ok(result.Document)
            : Accepted($"/api/documents/{result.Document.Id}", result.Document);
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        var doc = await documents.FindAsync(id, ct);
        var content = await store.DownloadAsync(doc.ObjectKey, ct);
        Response.RegisterForDisposeAsync(content);
        return File(
            System.IO.File.OpenRead(content.Path),
            doc.ContentType,
            doc.FileName,
            enableRangeProcessing: true
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
