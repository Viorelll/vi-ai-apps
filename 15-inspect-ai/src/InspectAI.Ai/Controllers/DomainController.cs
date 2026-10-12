using InspectAI.Domain.DTOs;
using InspectAI.Infrastructure.Ai;
using Microsoft.AspNetCore.Mvc;

namespace InspectAI.Ai.Controllers;

[ApiController, Route("internal/domain")]
public sealed class DomainController(FoundryClient client) : ControllerBase
{
    [HttpPost("step")]
    public Task<AiResult> Step(AiRequest input, CancellationToken ct) =>
        client.ExecuteAsync(input, ct);
}
