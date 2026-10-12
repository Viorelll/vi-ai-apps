namespace VideoStudioAI.Contracts.Dto;

public sealed record Storyboard(
    string Title,
    Scene[] Scenes,
    long InputTokens = 0,
    long OutputTokens = 0
);
