using EvalAI.Domain.DTOs;
using EvalAI.Services.Documents;
using Microsoft.AspNetCore.Mvc;
namespace EvalAI.Api.Controllers;

[ApiController, Route("api/chat")]
public sealed class ChatController(ChatService chat) : ControllerBase
{
    [HttpPost] public async Task<IActionResult> Ask(ChatRequest r, CancellationToken ct) => Ok(await chat.AskAsync(r, ct));
}
