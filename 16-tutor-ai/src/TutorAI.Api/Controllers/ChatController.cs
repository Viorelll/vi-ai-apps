using Microsoft.AspNetCore.Mvc;
using TutorAI.Domain.DTOs;
using TutorAI.Services.Tutoring;

namespace TutorAI.Api.Controllers;

[ApiController, Route("api/chat")]
public sealed class ChatController(TutorService tutor) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Chat(ChatRequest r, CancellationToken ct) =>
        Accepted(
            new
            {
                jobId = await tutor.QueueAsync(r.SessionId, "explain", r.Question, r.Revision, ct),
            }
        );
}
