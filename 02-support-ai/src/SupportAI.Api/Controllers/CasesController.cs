using Microsoft.AspNetCore.Mvc;
using SupportAI.Application.Services;
using SupportAI.Domain.DTOs;
using SupportAI.Domain.Entities;
namespace SupportAI.Api.Controllers;

[ApiController]
[Route("api/support/cases")]
public sealed class CasesController(SupportService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SupportCase>> Create(CreateCase request, CancellationToken ct)
    {
        var item = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new
        {
            id = item.Id
        }, item);
    }
    [HttpGet("{id:guid}")] public Task<CaseDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost("{id:guid}/attachments")]
    public async Task<IActionResult> Attach(Guid id, AttachDocument request, CancellationToken ct)
    {
        await service.AttachAsync(id, request.DocumentId, ct);
        return NoContent();
    }
    [HttpPost("{id:guid}/messages")] public async Task<IActionResult> Send(Guid id, SendMessage request, CancellationToken ct) => Accepted($"/api/support/cases/{id}", await service.SendAsync(id, request, ct));
}
