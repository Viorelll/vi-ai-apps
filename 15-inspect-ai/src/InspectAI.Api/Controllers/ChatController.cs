using InspectAI.Domain.DTOs;
using InspectAI.Services.Inspections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InspectAI.Api.Controllers;

[ApiController, Route("api/chat")]
public sealed class ChatController(InspectionService service) : ControllerBase
{
    [HttpPost, EnableRateLimiting("ai")]
    public async Task<IActionResult> Ask(ChatRequest r, CancellationToken ct) =>
        Ok(
            await service.QuestionAsync(
                r.InspectionId,
                new(r.Question, r.RequestId, r.Revision),
                ct
            )
        );
}
