using System.Text.Json;
using InspectAI.Domain.DTOs;
using InspectAI.Infrastructure.Abstractions;
using InspectAI.Infrastructure.Extraction;

namespace InspectAI.Tests.Fakes;

public sealed class DeterministicAiClient(DocumentExtractor extractor) : IAiClient
{
    public int Calls { get; private set; }

    public Task<ExtractionResult> StartAsync(ExtractionStart r, CancellationToken ct) =>
        extractor.StartAsync(r, ct);

    public Task<ExtractionResult> PollAsync(ExtractionPoll r, CancellationToken ct) =>
        extractor.PollAsync(r, ct);

    public Task<AiResult> StepAsync(AiRequest r, CancellationToken ct)
    {
        Calls++;
        object result;
        var s = r.Sources.First();
        if (r.Step == "question")
            result = new QuestionResult(
                "Verifică pata conform manualului.",
                [new(s.ChunkId, s.Text)],
                [r.Photos[0].DocumentId]
            );
        else
        {
            var finding = Guid.NewGuid();
            result = new InspectionResult(
                "Fotografia artificială arată o pată brună.",
                ["Coroziunea internă și siguranța nu pot fi stabilite din imagine."],
                [
                    new(
                        finding,
                        r.Photos[0].DocumentId,
                        "visible",
                        "Pată brună pe suprafața carcasei.",
                        null,
                        new("normalized", 0.3, 0.3, 0.2, 0.2)
                    ),
                ],
                [
                    new(
                        Guid.NewGuid(),
                        finding,
                        "Revizuiește secțiunea de curățare/inspecție cu un tehnician.",
                        [new(s.ChunkId, s.Text)]
                    ),
                ]
            );
        }
        return Task.FromResult(
            new AiResult(
                JsonSerializer.Serialize(
                    result,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)
                ),
                "test-only",
                10,
                20,
                1
            )
        );
    }
}
