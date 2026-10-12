using Microsoft.AspNetCore.Mvc;
using SupportAI.Application.Services;
using SupportAI.Domain.DTOs;
using SupportAI.Domain.Entities;
namespace SupportAI.Api.Controllers;

[ApiController]
[Route("api/support/actions")]
public sealed class ActionsController(ActionService service) : ControllerBase
{
    [HttpPost("{id:guid}/confirm")] public Task<ActionExecution> Confirm(Guid id, ConfirmAction request, CancellationToken ct) => service.ConfirmAsync(id, request, ct);
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
    {
        await service.RejectAsync(id, ct);
        return NoContent();
    }
}
