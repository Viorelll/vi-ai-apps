using EvalAI.Domain.DTOs;
using EvalAI.Infrastructure.Ai;
using Microsoft.AspNetCore.Mvc;
namespace EvalAI.Ai.Controllers;

[ApiController, Route("internal/chat")]
public sealed class ChatController(FoundryClient foundry) : ControllerBase
{
    [HttpPost] public async Task<IActionResult> Ask(ChatAiRequest r, CancellationToken ct) => Ok(await foundry.ChatAsync(r, ct));
}
