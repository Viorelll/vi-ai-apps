using InspectAI.Domain.DTOs;

namespace InspectAI.Infrastructure.Abstractions;

public interface IAiClient
{
    Task<AiResult> StepAsync(AiRequest request, CancellationToken ct);
    Task<ExtractionResult> StartAsync(ExtractionStart request, CancellationToken ct);
    Task<ExtractionResult> PollAsync(ExtractionPoll request, CancellationToken ct);
}
