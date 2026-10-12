using SupportAI.Domain.DTOs;
namespace SupportAI.Application.Abstractions;

public interface IAiGateway
{
    Task<DomainStepResult> StepAsync(DomainStepRequest request, CancellationToken ct);
    Task<ExtractionResult> StartAsync(ExtractionRequest request, CancellationToken ct);
    Task<ExtractionResult> PollAsync(string url, CancellationToken ct);
}
