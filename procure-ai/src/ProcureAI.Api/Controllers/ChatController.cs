using Microsoft.AspNetCore.Mvc;
using ProcureAI.Application.Services;
using ProcureAI.Domain.DTOs;

namespace ProcureAI.Api.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController(ChatService chat) : ControllerBase
{
    [HttpPost]
    public Task<ChatResultDto> Ask(ChatRequest request, CancellationToken ct) => chat.AskAsync(request, ct);
}
