using Microsoft.AspNetCore.Mvc;
using SupportAI.Application.Services;
using SupportAI.Domain.DTOs;
namespace SupportAI.Api.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController(SupportService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Ask(ChatRequest request, CancellationToken ct)
    {
        var id = request.CaseId ?? (await service.CreateAsync(new CreateCase(null), ct)).Id;
        var job = await service.SendAsync(id, new SendMessage(request.Question, Guid.NewGuid()), ct);
        return Accepted($"/api/support/cases/{id}", new
        {
            caseId = id,
            jobId = job.JobId
        });
    }
}
