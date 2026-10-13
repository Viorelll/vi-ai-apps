using AccessReadAI.Contracts;
using AccessReadAI.Services.Chat;
using Microsoft.AspNetCore.Mvc;

namespace AccessReadAI.Api.Controllers;

[ApiController, Route("api/chat")]
public sealed class ChatController(ChatService chat) : ControllerBase
{
    [HttpPost]
    public Task<object> Ask(ChatRequest request, CancellationToken ct) =>
        chat.AskAsync(request, ct);
}
