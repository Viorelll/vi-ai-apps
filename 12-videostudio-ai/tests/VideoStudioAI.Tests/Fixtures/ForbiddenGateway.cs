using VideoStudioAI.Contracts.Dto;
using VideoStudioAI.Services.Interfaces;
using VideoStudioAI.Services.Models;

namespace VideoStudioAI.Tests.Fixtures;

public sealed class ForbiddenGateway : IAiGateway
{
    public int Calls { get; private set; }

    private Task<T> Unexpected<T>()
    {
        Calls++;
        throw new InvalidOperationException("An ambiguous submit must not call the provider.");
    }

    public Task<Capabilities> CapabilitiesAsync(CancellationToken ct) => Unexpected<Capabilities>();

    public Task<Storyboard> StoryboardAsync(string brief, CancellationToken ct) =>
        Unexpected<Storyboard>();

    public Task<ProviderJob> SubmitAsync(VideoSubmit input, CancellationToken ct) =>
        Unexpected<ProviderJob>();

    public Task<ProviderJob> PollAsync(string id, CancellationToken ct) =>
        Unexpected<ProviderJob>();

    public Task<StoredObject> DownloadAsync(string id, CancellationToken ct) =>
        Unexpected<StoredObject>();

    public Task<ExtractResult> ExtractAsync(ExtractRequest input, CancellationToken ct) =>
        Unexpected<ExtractResult>();

    public Task<GroundedAnswer> AnswerAsync(GroundedRequest input, CancellationToken ct) =>
        Unexpected<GroundedAnswer>();
}
