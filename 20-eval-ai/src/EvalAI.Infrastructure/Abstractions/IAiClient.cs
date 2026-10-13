using EvalAI.Domain.DTOs;
namespace EvalAI.Infrastructure.Abstractions;

public interface IAiClient
{
    Task<AiStepResult> StepAsync(AiStepRequest request, CancellationToken ct);
    Task<ExtractionResult> StartAsync(ExtractionStart request, CancellationToken ct);
    Task<ExtractionResult> PollAsync(ExtractionPoll request, CancellationToken ct);
    Task<AiStepResult> ChatAsync(ChatAiRequest request, CancellationToken ct);
}
