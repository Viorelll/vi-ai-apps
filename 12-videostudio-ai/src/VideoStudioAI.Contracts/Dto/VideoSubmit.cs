namespace VideoStudioAI.Contracts.Dto;

public sealed record VideoSubmit(
    string Prompt,
    int Seconds,
    string Size,
    string Deployment,
    string? ParentProviderId = null
);
