using BuildBudgetAI.Api.Services;
using BuildBudgetAI.Domain.Dto;
using Microsoft.AspNetCore.Mvc;

namespace BuildBudgetAI.Api.Controllers;

[ApiController, Route("api/chat")]
public sealed class ChatController(ChatService chat) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Ask(ChatRequest r, CancellationToken ct) =>
        Ok(await chat.AskAsync(r, ct));
}
