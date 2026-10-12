using Microsoft.AspNetCore.Mvc;
using ProcureAI.Application.Abstractions;
using ProcureAI.Application.Exceptions;
using ProcureAI.Application.Services;
using ProcureAI.Domain.DTOs;

namespace ProcureAI.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(IProcurementRepository repository, DocumentService documents, IObjectStore objects) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok((await repository.ListDocumentsAsync(ct)).Select(DocumentDto.From));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        await repository.GetDocumentAsync(id, ct) is { State: not "Deleting" } document ? Ok(DocumentDto.From(document)) : NotFound();
    [HttpPost]
    [RequestSizeLimit(55L * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null) throw new AppException("file_required");
        var result = await documents.UploadAsync(file, ct);
        var body = new { documentId = result.Document.Id, state = result.Document.State, deduplicated = result.Existing };
        return result.Existing ? Ok(body) : AcceptedAtAction(nameof(Get), new { id = result.Document.Id }, body);
    }
    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        var document = await repository.GetDocumentAsync(id, ct);
        if (document is null || document.State == "Deleting") return NotFound();
        return File(await objects.OpenAsync(document.ObjectKey, ct), document.ContentType, document.FileName);
    }
    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    { await repository.RetryDocumentAsync(id, ct); return AcceptedAtAction(nameof(Get), new { id }, null); }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    { await repository.DeleteDocumentAsync(id, ct); return Accepted(); }
}
