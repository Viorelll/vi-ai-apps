using EvalAI.Domain.DTOs;
using EvalAI.Infrastructure.Ai;
using Microsoft.AspNetCore.Mvc;
namespace EvalAI.Ai.Controllers;

[ApiController, Route("internal/domain")]
public sealed class DomainController(FoundryClient foundry) : ControllerBase
{
    [HttpPost("step")] public async Task<IActionResult> Step(AiStepRequest r, CancellationToken ct) => Ok(await foundry.ExecuteAsync(r, ct));
}
