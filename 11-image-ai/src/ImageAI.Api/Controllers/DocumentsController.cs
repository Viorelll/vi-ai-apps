using ImageAI.Domain.Models;
using ImageAI.Infrastructure.Storage;
using ImageAI.Infrastructure.Validation;
using ImageAI.Services.Documents;
using Microsoft.AspNetCore.Mvc;

namespace ImageAI.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(DocumentService service, ObjectStore store) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(ImageValidator.MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file.Length is <= 0 or > ImageValidator.MaxBytes)
            throw new DomainException("file_size_limit", 413);
        // Bounded image-only uploads; non-image ingestion is an optional laboratory.
        await using var input = file.OpenReadStream();
        using var output = new MemoryStream();
        byte[] buffer = new byte[81920];
        int count;
        while ((count = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (output.Length + count > ImageValidator.MaxBytes)
                throw new DomainException("file_size_limit", 413);
            await output.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        var document = await service.UploadAsync(file.FileName, output.ToArray(), ct);
        return Ok(document);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(DocumentService.Map(await service.FindAsync(id, ct)));

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        var d = await service.FindAsync(id, ct);
        return File(
            await store.ReadAsync(d.ObjectKey, ct),
            d.ContentType,
            enableRangeProcessing: true
        );
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
