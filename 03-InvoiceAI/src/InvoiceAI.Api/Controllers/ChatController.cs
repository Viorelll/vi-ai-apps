using InvoiceAI.Contracts.Dto;
using InvoiceAI.Services.Services;
using Microsoft.AspNetCore.Mvc;
namespace InvoiceAI.Api.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController(ChatService service) : ControllerBase
{
    [HttpPost] public Task<ChatResponse> Ask(ChatRequest request, CancellationToken ct) => service.AskAsync(request, ct);
}
