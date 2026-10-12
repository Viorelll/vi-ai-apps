using VideoStudioAI.Contracts.Dto;
using VideoStudioAI.Services.Models;

namespace VideoStudioAI.Services.Interfaces;

public interface IAiGateway
{
    Task<Capabilities> CapabilitiesAsync(CancellationToken ct);
    Task<Storyboard> StoryboardAsync(string brief, CancellationToken ct);
    Task<ProviderJob> SubmitAsync(VideoSubmit input, CancellationToken ct);
    Task<ProviderJob> PollAsync(string id, CancellationToken ct);
    Task<StoredObject> DownloadAsync(string id, CancellationToken ct);
    Task<ExtractResult> ExtractAsync(ExtractRequest input, CancellationToken ct);
    Task<GroundedAnswer> AnswerAsync(GroundedRequest input, CancellationToken ct);
}
