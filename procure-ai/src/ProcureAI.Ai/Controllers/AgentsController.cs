using Microsoft.AspNetCore.Mvc;
using ProcureAI.Ai.Providers;
using ProcureAI.Application.Exceptions;
using ProcureAI.Domain.DTOs;

namespace ProcureAI.Ai.Controllers;

[ApiController]
[Route("internal/domain")]
public sealed class AgentsController(DemoAgentProvider demo, FoundryAgentProvider foundry, IConfiguration configuration) : ControllerBase
{
    [HttpPost("step")]
    public async Task<DomainStepResult> Step(DomainStepRequest request, CancellationToken ct)
    {
        if (request.Domain != "ProcureAI" || !AgentPrompts.Instructions.ContainsKey(request.Step) ||
            request.MaxOutputTokens is < 1 or > 1200 || request.Input.Sources.Any(s => !request.AllowedDocumentIds.Contains(s.DocumentId)))
            throw new AppException("invalid_agent_request");
        return configuration["Ai:Mode"] == "Demo" ? demo.Execute(request) : await foundry.ExecuteAsync(request, ct);
    }
}
