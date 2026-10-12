using ContractAI.Domain.DTOs;
using ContractAI.Services.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ContractAI.Api.Controllers;

[ApiController, Route("api/chat")]
public sealed class ChatController(ContractService contracts) : ControllerBase
{
    [HttpPost, EnableRateLimiting("ai")]
    public Task<AnswerDto> Ask(ChatRequest input, CancellationToken ct) =>
        contracts.QuestionAsync(
            input.VersionId,
            new QuestionRequest(input.RequestId, input.Question, input.ConversationId),
            ct
        );
}
