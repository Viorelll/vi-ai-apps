using ContractAI.Domain.DTOs;
using ContractAI.Infrastructure.Ai;
using Microsoft.AspNetCore.Mvc;

namespace ContractAI.Ai.Controllers;

[ApiController, Route("internal/domain")]
public sealed class DomainController(FoundryClient client) : ControllerBase
{
    [HttpPost("step")]
    public Task<AiResult> Step(AiRequest input, CancellationToken ct) =>
        client.ExecuteAsync(input, ct);
}
