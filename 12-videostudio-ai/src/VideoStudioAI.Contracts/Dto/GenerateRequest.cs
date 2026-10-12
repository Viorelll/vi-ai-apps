namespace VideoStudioAI.Contracts.Dto;

public sealed record GenerateRequest(
    long Revision,
    Guid RequestId,
    string SceneId,
    int Seconds,
    string Size
);
