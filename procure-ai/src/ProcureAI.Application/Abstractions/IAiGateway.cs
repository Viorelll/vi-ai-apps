namespace ProcureAI.Application.Abstractions;

using ProcureAI.Domain.DTOs;

public interface IAiGateway
{
    Task<DomainStepResult> StepAsync(DomainStepRequest request, CancellationToken ct);
    Task<ExtractionResult> ExtractAsync(ExtractionRequest request, CancellationToken ct);
}
