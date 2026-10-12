using System.Text;
using InvoiceAI.Contracts.Dto;
using InvoiceAI.Services.Services;
using Microsoft.AspNetCore.Mvc;
namespace InvoiceAI.Api.Controllers;

[ApiController]
[Route("api/invoices")]
public sealed class InvoicesController(InvoiceService service) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<InvoiceDto>> List(CancellationToken ct) => service.ListAsync(ct);
    [HttpGet("{id:guid}")] public Task<InvoiceDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost]
    public async Task<IActionResult> Create(CreateInvoiceRequest request, CancellationToken ct)
    {
        var i = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new
        {
            id = i.Id
        }, i);
    }
    [HttpPost("{id:guid}/analyze")] public async Task<IActionResult> Analyze(Guid id, CancellationToken ct) => Accepted(new { jobId = await service.AnalyzeAsync(id, ct) });
    [HttpPatch("{id:guid}/fields")] public Task<InvoiceDto> Correct(Guid id, CorrectionRequest request, CancellationToken ct) => service.CorrectAsync(id, request, ct);
    [HttpPost("{id:guid}/validate")] public Task<InvoiceDto> Validate(Guid id, CancellationToken ct) => service.ValidateAsync(id, ct);
    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct)
    {
        var snapshot = await service.ExportAsync(id, ct);
        Response.Headers.ETag = $"\"{snapshot.Hash}\"";
        return File(Encoding.UTF8.GetBytes(snapshot.PayloadJson), "application/json", $"invoice-{id}-r{snapshot.Revision}.json");
    }
}
