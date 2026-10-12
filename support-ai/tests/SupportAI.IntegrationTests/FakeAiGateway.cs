using SupportAI.Application.Abstractions;
using SupportAI.Domain.DTOs;
namespace SupportAI.IntegrationTests;

public sealed class FakeAiGateway : IAiGateway
{
    public Task<DomainStepResult> StepAsync(DomainStepRequest r, CancellationToken ct) => Task.FromResult(new DomainStepResult(new("Delivered; draft ticket only", "action_proposed", r.Sources.Select(x => x.Label).ToArray(), new("ticket", "Damaged monitor", r.Sources.Select(x => x.Label).ToArray())), "test-provider", 10, 20, 1));
    public Task<ExtractionResult> StartAsync(ExtractionRequest r, CancellationToken ct) => Task.FromResult(new ExtractionResult("Succeeded", null, [new("Visible damage; cause unknown", 1, null, null)], "{\"status\":\"Succeeded\"}"));
    public Task<ExtractionResult> PollAsync(string url, CancellationToken ct) => throw new NotSupportedException();
}
