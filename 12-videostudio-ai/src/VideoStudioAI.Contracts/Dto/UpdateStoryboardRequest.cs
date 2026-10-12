namespace VideoStudioAI.Contracts.Dto;

public sealed record UpdateStoryboardRequest(long Revision, Scene[] Scenes);
