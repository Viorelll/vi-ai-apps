using DocuAI.Application.Abstractions;
using DocuAI.Application.Exceptions;
using DocuAI.Application.Services;
using DocuAI.Domain.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DocuAI.Api.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController(ChatService chat) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] AskRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await chat.AskAsync(request, cancellationToken));
        }
        catch (ChatValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (AiAnswerException)
        {
            return Problem("Microsoft Foundry did not complete the request. Check the deployment and endpoint configuration.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (HttpRequestException)
        {
            return Problem("Microsoft Foundry is unavailable. Check network access and endpoint settings.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
