using Microsoft.AspNetCore.Mvc;
using VideoStudioAI.Contracts.Dto;
using VideoStudioAI.Services;

namespace VideoStudioAI.Api.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController(DocumentService documents) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Chat(ChatRequest request, CancellationToken ct) =>
        Ok(await documents.ChatAsync(request, ct));
}
