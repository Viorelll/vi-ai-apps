using ProcureAI.Ai.Providers;
using ProcureAI.Application.Abstractions;
using ProcureAI.Domain.DTOs;

namespace ProcureAI.Tests.Integration;

public sealed class DemoGateway : IAiGateway
{
    public Task<DomainStepResult> StepAsync(DomainStepRequest request, CancellationToken ct) => Task.FromResult(new DemoAgentProvider().Execute(request));
    public Task<ExtractionResult> ExtractAsync(ExtractionRequest request, CancellationToken ct) => throw new NotSupportedException();
}
