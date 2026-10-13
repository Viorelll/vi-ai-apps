using Microsoft.AspNetCore.Mvc;
using VideoSearchAI.Ai.Services;
using VideoSearchAI.Contracts.Dto;

namespace VideoSearchAI.Ai.Controllers;

[ApiController, Route("internal")]
public sealed class GatewayController(AiGatewayService gateway) : ControllerBase
{
    [HttpPost("extractions/start")]
    public Task<ExtractionResult> Start(ExtractionRequest r, CancellationToken ct) =>
        gateway.StartAsync(r, ct);

    [HttpPost("extractions/poll")]
    public Task<ExtractionResult> Poll(ExtractionPollRequest r, CancellationToken ct) =>
        gateway.PollAsync(r, ct);

    [HttpPost("index/upsert")]
    public async Task<object> Index(IndexRequest r, CancellationToken ct)
    {
        if (
            r.Segments.Length == 0
            || r.Segments.Length > 2000
            || r.Segments.Any(s =>
                s.DocumentId != r.DocumentId
                || s.VideoId != r.VideoId
                || s.Generation != r.Generation
            )
        )
            return BadRequest();
        await gateway.UpsertAsync(r, ct);
        return new { status = "ok" };
    }

    [HttpPost("index/delete")]
    public async Task<object> Delete(DeleteIndexRequest r, CancellationToken ct)
    {
        await gateway.DeleteAsync(r.DocumentId, ct);
        return new { status = "ok" };
    }

    [HttpPost("retrieve")]
    public Task<RetrievalHit[]> Retrieve(RetrieveRequest r, CancellationToken ct)
    {
        if (r.DocumentIds.Length > 50 || r.Question.Length > 2000)
            throw new Providers.ProviderException("retrieval_input_limit", 400);
        return gateway.RetrieveAsync(r, ct);
    }

    [HttpPost("generate")]
    public Task<GeneratedAnswer> Generate(GenerateRequest r, CancellationToken ct) =>
        gateway.GenerateAsync(r, ct);
}
