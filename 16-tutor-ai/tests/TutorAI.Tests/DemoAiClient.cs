using Microsoft.Extensions.Configuration;
using TutorAI.Domain.DTOs;
using TutorAI.Infrastructure.Abstractions;
using TutorAI.Infrastructure.Ai;
using TutorAI.Infrastructure.Extraction;

namespace TutorAI.Tests;

public sealed class DemoAiClient(IObjectStore storage) : IAiClient
{
    private static IConfiguration Config =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "Ai:DemoMode", "true" } })
            .Build();

    public Task<AiResult> StepAsync(AiRequest r, CancellationToken ct) =>
        new FoundryClient(new HttpClient(), Config).ExecuteAsync(r, ct);

    public Task<ExtractionResult> StartAsync(ExtractionStart r, CancellationToken ct) =>
        new DocumentExtractor(new HttpClient(), storage, Config).StartAsync(r, ct);

    public Task<ExtractionResult> PollAsync(ExtractionPoll r, CancellationToken ct) =>
        throw new NotSupportedException();
}
