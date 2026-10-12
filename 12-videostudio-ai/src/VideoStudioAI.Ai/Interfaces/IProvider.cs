using VideoStudioAI.Contracts.Dto;

namespace VideoStudioAI.Ai.Interfaces;

public interface IProvider
{
    Task<Storyboard> StoryboardAsync(string brief, CancellationToken ct);
    Task<ProviderJob> SubmitAsync(VideoSubmit input, CancellationToken ct);
    Task<ProviderJob> PollAsync(string id, CancellationToken ct);
    Task<string> DownloadAsync(string id, CancellationToken ct);
    Task<GroundedAnswer> AnswerAsync(GroundedRequest input, CancellationToken ct);
    Task<string> DescribeImageAsync(string contentType, string base64, CancellationToken ct);
}
