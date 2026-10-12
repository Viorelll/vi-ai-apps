using ContractAI.Domain.DTOs;
using ContractAI.Infrastructure.Extraction;
using Microsoft.AspNetCore.Mvc;

namespace ContractAI.Ai.Controllers;

[ApiController, Route("internal/extractions")]
public sealed class ExtractionsController(DocumentExtractor extractor) : ControllerBase
{
    [HttpPost("start")]
    public Task<ExtractionResult> Start(ExtractionStart input, CancellationToken ct) =>
        extractor.StartAsync(input, ct);

    [HttpPost("poll")]
    public Task<ExtractionResult> Poll(ExtractionPoll input, CancellationToken ct) =>
        extractor.PollAsync(input, ct);
}
