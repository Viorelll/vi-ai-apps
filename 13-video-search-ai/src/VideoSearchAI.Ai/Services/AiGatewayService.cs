using Microsoft.Extensions.Options;
using VideoSearchAI.Ai.Options;
using VideoSearchAI.Ai.Providers;
using VideoSearchAI.Ai.Providers.Extraction;
using VideoSearchAI.Ai.Providers.Foundry;
using VideoSearchAI.Ai.Providers.Search;
using VideoSearchAI.Contracts.Dto;

namespace VideoSearchAI.Ai.Services;

public sealed class AiGatewayService(
    IOptions<AiOptions> options,
    DemoExtractor demo,
    ContentUnderstandingClient cu,
    DemoRetrievalProvider local,
    AzureSearchProvider search,
    AnswerGenerator generator
)
{
    private bool IsDemo => options.Value.Mode == "Demo";

    public Task<ExtractionResult> StartAsync(ExtractionRequest r, CancellationToken ct) =>
        IsDemo ? Task.FromResult(demo.Extract(r)) : cu.StartAsync(r, ct);

    public Task<ExtractionResult> PollAsync(ExtractionPollRequest r, CancellationToken ct) =>
        IsDemo ? throw new ProviderException("demo_poll_not_supported", 400) : cu.PollAsync(r, ct);

    public Task UpsertAsync(IndexRequest r, CancellationToken ct) =>
        IsDemo ? local.UpsertAsync(r, ct) : search.UpsertAsync(r, ct);

    public Task DeleteAsync(Guid id, CancellationToken ct) =>
        IsDemo ? local.DeleteAsync(id, ct) : search.DeleteAsync(id, ct);

    public Task<RetrievalHit[]> RetrieveAsync(RetrieveRequest r, CancellationToken ct) =>
        IsDemo ? local.RetrieveAsync(r, ct) : search.RetrieveAsync(r, ct);

    public Task<GeneratedAnswer> GenerateAsync(GenerateRequest r, CancellationToken ct)
    {
        if (!IsDemo)
            return generator.GenerateAsync(r, ct);
        var source = r.Sources.FirstOrDefault();
        return Task.FromResult(
            source == null
                ? new GeneratedAnswer(
                    "Dovezile disponibile nu acoperă această întrebare.",
                    [],
                    0,
                    0,
                    "demo-fixture"
                )
                : new GeneratedAnswer(
                    source.Segment.Transcript.Length > 0
                        ? source.Segment.Transcript
                        : source.Segment.VisualText!,
                    [
                        new(
                            source.Label,
                            source.Segment.VideoId,
                            source.Segment.Id,
                            source.Segment.Transcript.Length > 0 ? "transcript" : "visual"
                        ),
                    ],
                    0,
                    0,
                    "demo-fixture"
                )
        );
    }
}
