using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VideoStudioAI.Ai.Interfaces;
using VideoStudioAI.Ai.Models;
using VideoStudioAI.Ai.Services;
using VideoStudioAI.Contracts.Dto;

namespace VideoStudioAI.Ai.Controllers;

[ApiController]
[Route("internal")]
public sealed class InternalController(
    IProvider provider,
    ExtractionService extraction,
    IOptions<FoundryOptions> config
) : ControllerBase
{
    [HttpGet("capabilities")]
    public IActionResult Capabilities() =>
        Ok(
            new Capabilities(
                config.Value.Mode,
                config.Value.VideoDeployment,
                config.Value.Seconds,
                config.Value.Sizes,
                config.Value.VideoEnabled
            )
        );

    [HttpPost("storyboard")]
    public async Task<IActionResult> Storyboard([FromBody] BriefRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Brief) || r.Brief.Length > 8000)
            throw new GatewayException(
                400,
                "invalid_brief",
                "Brief must contain 1–8000 characters."
            );
        return Ok(await provider.StoryboardAsync(r.Brief, ct));
    }

    [HttpPost("videos")]
    public async Task<IActionResult> Submit(VideoSubmit input, CancellationToken ct)
    {
        if (!config.Value.VideoEnabled)
            throw new GatewayException(503, "video_disabled", "Video generation disabled.");
        if (
            !config.Value.Seconds.Contains(input.Seconds)
            || !config.Value.Sizes.Contains(input.Size)
            || input.Deployment != config.Value.VideoDeployment
            || string.IsNullOrWhiteSpace(input.Prompt)
            || input.Prompt.Length > 4000
        )
            throw new GatewayException(
                400,
                "unsupported_parameters",
                "Unsupported video parameters."
            );
        return Ok(await provider.SubmitAsync(input, ct));
    }

    [HttpGet("videos/{id}")]
    public async Task<IActionResult> Poll(string id, CancellationToken ct) =>
        Ok(await provider.PollAsync(id, ct));

    [HttpGet("videos/{id}/content")]
    public async Task<IActionResult> Content(string id, CancellationToken ct)
    {
        var path = await provider.DownloadAsync(id, ct);
        Response.OnCompleted(() =>
        {
            System.IO.File.Delete(path);
            return Task.CompletedTask;
        });
        return File(System.IO.File.OpenRead(path), "video/mp4");
    }

    [HttpPost("extract")]
    [RequestSizeLimit(73400320)]
    public async Task<IActionResult> Extract(ExtractRequest input, CancellationToken ct) =>
        Ok(await extraction.ExtractAsync(input, ct));

    [HttpPost("chat")]
    public async Task<IActionResult> Chat(GroundedRequest input, CancellationToken ct)
    {
        if (
            string.IsNullOrWhiteSpace(input.Question)
            || input.Question.Length > 2000
            || input.Sources.Length > 8
            || input.Sources.Any(s => s.Excerpt.Length > 1200)
        )
            throw new GatewayException(400, "invalid_chat", "Invalid source scope or question.");
        return Ok(await provider.AnswerAsync(input, ct));
    }
}
