namespace VideoStudioAI.Contracts.Dto;

public sealed record ClipDto(
    Guid Id,
    Guid JobId,
    string ContentUrl,
    int DurationMs,
    int Width,
    int Height,
    string Sha256,
    string Prompt,
    string Deployment,
    Guid? SourceClipId
);
