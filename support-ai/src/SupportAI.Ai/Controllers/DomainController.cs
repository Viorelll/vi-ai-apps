using Microsoft.AspNetCore.Mvc;
using SupportAI.Ai.Services;
using SupportAI.Domain.DTOs;
using SupportAI.Domain.Models;
namespace SupportAI.Ai.Controllers;

[ApiController]
[Route("internal/domain")]
public sealed class DomainController(FoundrySupportService service, PromptShieldService safety) : ControllerBase
{
    [HttpPost("step")]
    public async Task<ActionResult<DomainStepResult>> Step(DomainStepRequest request, CancellationToken ct)
    {
        if (request.Text.Length > 8000 || request.Sources.Count > 8 || request.History.Count > 10 || request.Sources.Any(s => s.Text.Length > 3000))
            throw new SupportException("ai_input_limit");
        await safety.CheckAsync(request, ct);
        return await service.RunAsync(request, ct);
    }
}
