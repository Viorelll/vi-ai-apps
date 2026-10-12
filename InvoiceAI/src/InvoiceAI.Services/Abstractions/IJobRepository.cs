using InvoiceAI.Contracts.Dto;
using InvoiceAI.Domain.Entities;
namespace InvoiceAI.Services.Abstractions;

public interface IJobRepository
{
    Task<IngestionJob?> ClaimAsync(CancellationToken ct);
    Task SchedulePollAsync(IngestionJob job, string operation, int seconds, CancellationToken ct);
    Task ActivateAsync(IngestionJob job, ExtractionDto extraction, string rawKey, CancellationToken ct);
    Task CompleteAsync(IngestionJob job, ExplanationDto explanation, CancellationToken ct);
    Task FinishCleanupAsync(IngestionJob job, CancellationToken ct);
    Task FailAsync(IngestionJob job, string code, bool transient, int delaySeconds, CancellationToken ct, long inputTokens = 0, long outputTokens = 0);
}
