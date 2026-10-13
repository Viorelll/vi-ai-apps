using AccessReadAI.Ai.Providers;
using AccessReadAI.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AccessReadAI.Ai.Controllers;

[ApiController, Route("internal")]
public sealed class PipelineController(
    LayoutExtractor extraction,
    AccessibleTransformer transform,
    SpeechClient speech
) : ControllerBase
{
    [HttpPost("extract")]
    public Task<ExtractionResult> Extract(ExtractionRequest request, CancellationToken ct) =>
        extraction.ExtractAsync(request, ct);

    [HttpPost("transform")]
    public Task<TransformResult> Transform(TransformRequest request, CancellationToken ct) =>
        transform.TransformAsync(request, ct);

    [HttpPost("narrate")]
    public Task<NarrateResult> Narrate(NarrateRequest request, CancellationToken ct) =>
        speech.NarrateAsync(request, ct);

    [HttpPost("answer")]
    public Task<GroundedAnswer> Answer(GroundedRequest request, CancellationToken ct) =>
        transform.AnswerAsync(request, ct);
}
