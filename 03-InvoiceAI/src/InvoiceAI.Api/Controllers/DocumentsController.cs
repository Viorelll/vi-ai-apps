using InvoiceAI.Contracts.Dto;
using InvoiceAI.Domain.Entities;
using InvoiceAI.Services.Services;
using Microsoft.AspNetCore.Mvc;
namespace InvoiceAI.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(DocumentService service) : ControllerBase
{
    private static DocumentDto Dto(Document d) => new(d.Id, d.FileName, d.ContentType, d.SizeBytes, d.Status, d.ActiveGeneration);
    [HttpGet] public async Task<IReadOnlyList<DocumentDto>> List(CancellationToken ct) => (await service.ListAsync(ct)).Select(Dto).ToArray();
    [HttpGet("{id:guid}")] public async Task<DocumentDto> Get(Guid id, CancellationToken ct) => Dto(await service.GetAsync(id, ct));
    [HttpPost]
    [RequestSizeLimit(55L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50L * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        await using var input = file.OpenReadStream();
        var response = await service.UploadAsync(input, file.FileName, file.ContentType, ct);
        return response.Duplicate ? Ok(response) : Accepted(response);
    }
    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        var (d, s) = await service.OpenAsync(id, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "no-store";
        Response.GetTypedHeaders().ContentDisposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("inline") { FileNameStar = d.FileName };
        return File(s, d.ContentType);
    }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => Accepted(new { jobId = await service.DeleteAsync(id, ct) });
    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        await service.RetryAsync(id, ct);
        return Accepted();
    }
}
