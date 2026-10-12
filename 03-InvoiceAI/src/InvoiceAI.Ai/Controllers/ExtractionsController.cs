using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using InvoiceAI.Ai.Configuration;
using InvoiceAI.Ai.Providers.Extraction;
using InvoiceAI.Contracts.Dto;
namespace InvoiceAI.Ai.Controllers;

[ApiController]
[Route("internal/extractions")]
public sealed class ExtractionsController(DocumentIntelligenceClient live, DemoExtractionProvider demo, IOptions<ProviderOptions> options) : ControllerBase
{
    [HttpPost("start")] public Task<StartExtractionResponse> Start(StartExtractionRequest r, CancellationToken ct) => options.Value.Mode == "Demo" ? demo.StartAsync(r, ct) : live.StartAsync(r, ct);
    [HttpPost("poll")] public Task<PollExtractionResponse> Poll(PollExtractionRequest r, CancellationToken ct) => options.Value.Mode == "Demo" ? demo.PollAsync(r, ct) : live.PollAsync(r, ct);
}
