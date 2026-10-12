namespace VideoStudioAI.Contracts.Dto;

public sealed record Capabilities(
    string Mode,
    string Deployment,
    int[] Seconds,
    string[] Sizes,
    bool VideoEnabled,
    bool Preview = true
);
